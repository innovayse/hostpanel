using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <see cref="ClientQueryExtensions"/>, the include list every "load the whole client" use case
/// shares, run against the real PostgreSQL host.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientQueryExtensionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a client with one contact and returns its identity subject and id.</summary>
    /// <returns>The subject the client is linked to, and the client id.</returns>
    private async Task<SeededClient> SeedClientWithContactAsync()
    {
        var userId = $"user-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create(userId, "Ada", "Lovelace", $"{userId}@example.test", DateTimeOffset.UtcNow);
        client.AddContact(new ContactDetails(
            "Grace", "Hopper", null, $"contact-{userId}@example.test", null, ContactType.Billing,
            new PostalAddress(null, null, null, null, null, null),
            new ContactNotifications(true, true, true, true, true, true)));
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return new(client.Id, userId);
    }

    /// <summary>The shared include list brings the contacts along with the client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IncludeChildren_LoadsTheContactsAsync()
    {
        var (clientId, _) = await SeedClientWithContactAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var client = await context.Clients.IncludeChildren().FirstAsync(c => c.Id == clientId);

        client.Contacts.Should().ContainSingle().Which.FirstName.Should().Be("Grace");
    }
}
