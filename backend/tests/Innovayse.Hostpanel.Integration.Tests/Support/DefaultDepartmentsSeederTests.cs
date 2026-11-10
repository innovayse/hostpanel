using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Support.Seeding;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Tests for <see cref="DefaultDepartmentsSeeder"/> against Postgres: it creates the standard
/// departments once on an empty table and never touches one an operator has already changed.
/// </summary>
/// <remarks>
/// <c>Program.cs</c> skips this seeder entirely in the
/// <c>Testing</c> environment (migrations and seeding there are the test factory's own job), so
/// these tests call it directly against a real database. This class's factory has a database of its
/// own (no <c>ICollectionFixture</c> in this suite), so clearing the <c>Departments</c> table here
/// cannot affect another class's assertions.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DefaultDepartmentsSeederTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deletes every row in the <c>Departments</c> table so a test starts from empty.</summary>
    /// <param name="context">The data context to clear.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private static async Task ClearDepartmentsAsync(IHostpanelDbContext context)
    {
        await context.Departments.ExecuteDeleteAsync();
    }

    /// <summary>An empty table gets every default department created exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeededAsync_EmptyTable_CreatesEveryDefaultOnceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DefaultDepartmentsOptions>>().Value;
        await ClearDepartmentsAsync(context);

        var created = await DefaultDepartmentsSeeder.EnsureSeededAsync(context, options);

        Assert.Equal(4, created);
        var names = await context.Departments.AsNoTracking().Select(d => d.Name).ToListAsync();
        Assert.Equal(
            new[] { "Technical Support", "Billing", "Sales", "General" }.OrderBy(n => n),
            names.OrderBy(n => n));
    }

    /// <summary>
    /// Nothing is created, and an operator's own department survives untouched, when the table
    /// already holds at least one row: the seeder is keyed on "the table is empty", not on names.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeededAsync_TableAlreadyHasARow_CreatesNothingAndLeavesItAloneAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DefaultDepartmentsOptions>>().Value;
        await ClearDepartmentsAsync(context);
        context.Departments.Add(Department.Create("Renamed Support Desk", "renamed@example.test"));
        await context.SaveChangesAsync(CancellationToken.None);

        var created = await DefaultDepartmentsSeeder.EnsureSeededAsync(context, options);

        Assert.Equal(0, created);
        var department = await context.Departments.AsNoTracking().SingleAsync();
        Assert.Equal("Renamed Support Desk", department.Name);
    }

    /// <summary>
    /// Two instances starting together both seed an empty table: whichever loses the race to the unique
    /// index on the name carries on instead of failing the start, and the departments exist exactly once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeededAsync_TwoConcurrentStarts_BothSucceedAndCreateEachDepartmentOnceAsync()
    {
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var options = first.ServiceProvider.GetRequiredService<IOptions<DefaultDepartmentsOptions>>().Value;
        await ClearDepartmentsAsync(firstContext);

        var results = await Task.WhenAll(
            DefaultDepartmentsSeeder.EnsureSeededAsync(firstContext, options),
            DefaultDepartmentsSeeder.EnsureSeededAsync(secondContext, options));

        Assert.Equal(4, results.Sum());
        Assert.Equal(4, await firstContext.Departments.AsNoTracking().CountAsync());
    }

    /// <summary>The configured addresses, not built-in literals, become the departments' contact emails.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeededAsync_ConfiguredAddresses_AreUsedAsDepartmentEmailsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await ClearDepartmentsAsync(context);
        var options = new DefaultDepartmentsOptions
        {
            SupportEmail = "tech@example.test",
            BillingEmail = "money@example.test",
            SalesEmail = "buy@example.test",
            GeneralEmail = "info@example.test",
        };

        await DefaultDepartmentsSeeder.EnsureSeededAsync(context, options);

        var emails = await context.Departments.AsNoTracking().Select(d => d.Email).ToListAsync();
        Assert.Equal(
            new[] { "tech@example.test", "money@example.test", "buy@example.test", "info@example.test" }.OrderBy(e => e),
            emails.OrderBy(e => e));
    }
}
