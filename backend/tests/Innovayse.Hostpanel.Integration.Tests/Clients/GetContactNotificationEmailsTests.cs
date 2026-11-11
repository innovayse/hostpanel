using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Queries.GetContactNotificationEmails;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <see cref="GetContactNotificationEmailsQuery"/> through the real <see cref="IMessageBus"/>: the
/// query the mail sender dispatches to copy a message to a client's contacts.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetContactNotificationEmailsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Only the contacts who switched the kind of email on are returned, each address once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ReturnsOnlyTheContactsWhoWantThatKindAsync()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var wanting = $"wants-{Guid.NewGuid():N}@example.test";
        var declining = $"declines-{Guid.NewGuid():N}@example.test";
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Wants", wanting, true))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Declines", declining, false))).EnsureSuccessStatusCode();

        var invoice = await InvokeAsync(new GetContactNotificationEmailsQuery(clientId, ContactNotification.Invoice));
        var support = await InvokeAsync(new GetContactNotificationEmailsQuery(clientId, ContactNotification.Support));

        invoice.Value.Should().Equal(wanting);
        support.Value.Should().BeEquivalentTo(new[] { wanting, declining });
    }

    /// <summary>A client with no contacts, or an unknown id, has nobody to copy: an empty list, not a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoContacts_ReturnsAnEmptyListAsync()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);

        var known = await InvokeAsync(new GetContactNotificationEmailsQuery(clientId, ContactNotification.Domain));
        var unknown = await InvokeAsync(new GetContactNotificationEmailsQuery(int.MaxValue, ContactNotification.Domain));

        known.IsSuccess.Should().BeTrue();
        known.Value.Should().BeEmpty();
        unknown.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a scope of the shared host.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's answer.</returns>
    private async Task<Result<IReadOnlyList<string>>> InvokeAsync(GetContactNotificationEmailsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<string>>>(query, CancellationToken.None);
    }
}
