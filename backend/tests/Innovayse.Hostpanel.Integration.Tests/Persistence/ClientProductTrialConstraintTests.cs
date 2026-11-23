using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The unique index on <c>ClientProductTrials</c> against Postgres: a client has one trial of a product, ever. The rows
/// are written with raw SQL, around the aggregate, to prove the database refuses the second one.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientProductTrialConstraintTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The Postgres state code of a unique violation.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>A second trial of the same product for the same client is refused by the unique index.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheSameClientAndProductCannotBeInsertedTwice()
    {
        await InsertAsync(clientId: 9001, productId: 9002);

        var act = () => InsertAsync(clientId: 9001, productId: 9002);

        var error = (await act.Should().ThrowAsync<PostgresException>()).Which;
        error.SqlState.Should().Be(UniqueViolation);
        error.ConstraintName.Should().Be("IX_ClientProductTrials_ClientId_ProductId");
    }

    /// <summary>Another product, or another client, is a different trial.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADifferentClientOrProduct_IsAllowed()
    {
        await InsertAsync(clientId: 9101, productId: 9102);

        (await InsertAsync(clientId: 9101, productId: 9103)).Should().Be(1);
        (await InsertAsync(clientId: 9104, productId: 9102)).Should().Be(1);
    }

    /// <summary>Inserts one trial row straight into the table.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="productId">The product.</param>
    /// <returns>The number of rows inserted.</returns>
    private async Task<int> InsertAsync(int clientId, int productId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "ClientProductTrials" ("ClientId", "ProductId", "StartedAt") VALUES ({clientId}, {productId}, {DateTimeOffset.UtcNow})""");
    }
}
