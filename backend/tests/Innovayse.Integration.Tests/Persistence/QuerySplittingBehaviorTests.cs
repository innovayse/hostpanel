namespace Innovayse.Integration.Tests.Persistence;

using FluentAssertions;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pins the composition root's EF Core query-splitting default.
/// </summary>
/// <remarks>
/// Several repositories load more than one collection navigation in a single query —
/// <c>DomainRepository</c> alone includes four — and EF Core's default is one SQL
/// statement whose row count is the product of those collections. Production logged
/// EF's warning about it on every domain cron run. The fix is a default in
/// <c>Infrastructure/DependencyInjection.cs</c>, and this test is what would notice if
/// that line were lost in a later edit of the options builder.
/// </remarks>
public sealed class QuerySplittingBehaviorTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The registered <see cref="AppDbContext"/> options split multi-collection queries.</summary>
    [Fact]
    public void DbContextOptions_SplitQueriesByDefault()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();

        // Act
        var relational = options.Extensions.OfType<RelationalOptionsExtension>().Single();

        // Assert
        relational.QuerySplittingBehavior.Should().Be(QuerySplittingBehavior.SplitQuery);
    }
}
