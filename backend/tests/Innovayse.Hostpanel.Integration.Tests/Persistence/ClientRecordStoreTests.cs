using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins <see cref="IClientRecordStore"/>: two writers saving a client record for the same user
/// end with one row and no failure, which is what the first order of a freshly registered caller
/// relies on when the registration handler has not yet run, or runs at the same moment.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientRecordStoreTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A record saved while another writer already holds the user's row yields that row, not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddOrGetReturnsTheExistingRecordWhenTheUserAlreadyHasOne()
    {
        // Arrange: the first writer has saved its record.
        var userId = $"race-{Guid.NewGuid():N}";
        using (var first = factory.Services.CreateScope())
        {
            var saved = await first.ServiceProvider.GetRequiredService<IClientRecordStore>()
                .AddOrGetAsync(Client.Create(userId, "Grace", "Hopper", $"{userId}@example.test", DateTimeOffset.UtcNow), CancellationToken.None);
            saved.Created.Should().BeTrue();
        }

        // Act: the second writer lost the race, so its insert hits the unique index on the user.
        using var second = factory.Services.CreateScope();
        var loser = Client.Create(userId, "Grace", "Hopper", $"{userId}@example.test", DateTimeOffset.UtcNow);
        var outcome = await second.ServiceProvider.GetRequiredService<IClientRecordStore>()
            .AddOrGetAsync(loser, CancellationToken.None);

        // Assert: it got the winner's row, one row exists, and the context can still save.
        outcome.Created.Should().BeFalse();
        outcome.Client.UserId.Should().Be(userId);
        outcome.Client.Id.Should().BePositive();

        var db = second.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Clients.CountAsync(c => c.UserId == userId)).Should().Be(1);
        await db.Invoking(d => d.SaveChangesAsync()).Should().NotThrowAsync();
    }

    /// <summary>A record for a user nobody has saved yet is stored and reported as created.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddOrGetSavesTheCandidateWhenTheUserHasNoRecord()
    {
        // Arrange
        var userId = $"fresh-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();

        // Act
        var outcome = await scope.ServiceProvider.GetRequiredService<IClientRecordStore>()
            .AddOrGetAsync(Client.Create(userId, "Ada", "Lovelace", $"{userId}@example.test", DateTimeOffset.UtcNow), CancellationToken.None);

        // Assert
        outcome.Created.Should().BeTrue();
        outcome.Client.Id.Should().BePositive();
    }
}
