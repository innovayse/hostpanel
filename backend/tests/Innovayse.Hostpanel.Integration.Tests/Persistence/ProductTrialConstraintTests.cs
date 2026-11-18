using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The check constraints on the free-trial columns of <c>Products</c> against Postgres. The aggregate already refuses
/// these values, so the cases write them with raw SQL, around the aggregate, to prove the database refuses them too and
/// names the constraint that did.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProductTrialConstraintTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The Postgres state code of a check constraint violation.</summary>
    private const string CheckViolation = "23514";

    /// <summary>The bounds themselves are stored, so the refusals below are the constraints and not the update.</summary>
    /// <param name="days">A trial length inside the bounds.</param>
    /// <param name="grace">A grace period inside the bounds.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(365, 30)]
    public async Task Bounds_AreStored(int days, int grace)
    {
        var id = await SeedProductAsync();

        var affected = await UpdateAsync(id, days, grace);

        affected.Should().Be(1);
    }

    /// <summary>A trial shorter than a day or longer than a year is refused.</summary>
    /// <param name="days">The length to write.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public async Task TrialDaysOutOfRange_IsRefused(int days)
    {
        var id = await SeedProductAsync();

        var act = () => UpdateAsync(id, days, 3);

        await AssertRefusedAsync(act, "CK_Products_TrialDays");
    }

    /// <summary>A negative grace period or one over thirty days is refused.</summary>
    /// <param name="grace">The grace period to write.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task TrialGracePeriodOutOfRange_IsRefused(int grace)
    {
        var id = await SeedProductAsync();

        var act = () => UpdateAsync(id, 14, grace);

        await AssertRefusedAsync(act, "CK_Products_TrialGracePeriodDays");
    }

    /// <summary>Creates a product through the API, its trial columns at their defaults.</summary>
    /// <returns>The new product's id.</returns>
    private async Task<int> SeedProductAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        return await ProductsTestSeeds.CreateProductAsync(admin);
    }

    /// <summary>Writes a trial length and grace period straight into the row.</summary>
    /// <param name="id">The product's id.</param>
    /// <param name="days">The trial length.</param>
    /// <param name="grace">The grace period.</param>
    /// <returns>The number of rows changed.</returns>
    private async Task<int> UpdateAsync(int id, int days, int grace)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Products" SET "TrialDays" = {days}, "TrialGracePeriodDays" = {grace} WHERE "Id" = {id}""");
    }

    /// <summary>Asserts the write is refused by the named check constraint.</summary>
    /// <param name="act">The write.</param>
    /// <param name="constraint">The constraint that must refuse it.</param>
    /// <returns>A task that completes once the assertion has run.</returns>
    private static async Task AssertRefusedAsync(Func<Task> act, string constraint)
    {
        var error = (await act.Should().ThrowAsync<PostgresException>()).Which;

        error.SqlState.Should().Be(CheckViolation);
        error.ConstraintName.Should().Be(constraint);
    }
}
