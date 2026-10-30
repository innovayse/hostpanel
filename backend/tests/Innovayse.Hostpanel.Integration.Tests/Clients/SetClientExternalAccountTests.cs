using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Clients.Commands.SetClientExternalAccount;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateMyProfile;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>PUT /api/clients/{id}/external-account</c>: setting and clearing a client's external account link.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetClientExternalAccountTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Setting the link writes both halves, trimmed, and answers 204.</summary>
    [Fact]
    public async Task SetWritesTheLink()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/external-account", new SetClientExternalAccountCommand(0, " crm ", " acct-set-1 "));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (provider, accountId) = await ReadLinkAsync(clientId);
        provider.Should().Be("crm");
        accountId.Should().Be("acct-set-1");
        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Set External Account");
    }

    /// <summary>Sending no provider and no account id clears an existing link and answers 204.</summary>
    [Fact]
    public async Task ClearRemovesTheLink()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/external-account", new SetClientExternalAccountCommand(0, "crm", "acct-clear-1")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/external-account", new SetClientExternalAccountCommand(0, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (provider, accountId) = await ReadLinkAsync(clientId);
        provider.Should().BeNull();
        accountId.Should().BeNull();
        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Cleared External Account");
    }

    /// <summary>Linking a pair another client already holds answers 409 and leaves this client unlinked.</summary>
    [Fact]
    public async Task SetRefusesAPairAnotherClientHolds()
    {
        var (first, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (second, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var body = new SetClientExternalAccountCommand(0, "crm", "acct-dupe-1");
        (await admin.PutAsJsonAsync($"/api/clients/{first}/external-account", body))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PutAsJsonAsync($"/api/clients/{second}/external-account", body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EXTERNAL_ACCOUNT_ALREADY_LINKED);
        var (provider, _) = await ReadLinkAsync(second);
        provider.Should().BeNull();
    }

    /// <summary>Writing the link a client already holds is not a conflict with itself.</summary>
    [Fact]
    public async Task SetAgainOnTheSameClientSucceeds()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var body = new SetClientExternalAccountCommand(0, "crm", "acct-same-1");
        (await admin.PutAsJsonAsync($"/api/clients/{clientId}/external-account", body))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}/external-account", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Only one half of the pair answers 400, since the link is whole or absent.</summary>
    [Fact]
    public async Task SetRefusesAHalfLink()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/external-account", new SetClientExternalAccountCommand(0, "crm", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task SetRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/external-account",
            new SetClientExternalAccountCommand(0, "crm", "acct-missing-1"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>A client, who is not staff, is refused with 403, even on their own record.</summary>
    [Fact]
    public async Task SetRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "external-forbidden");

        var response = await client.PutAsJsonAsync(
            $"/api/clients/{clientId}/external-account", new SetClientExternalAccountCommand(0, "crm", "acct-forbidden-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var (provider, _) = await ReadLinkAsync(clientId);
        provider.Should().BeNull();
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task SetRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/external-account", new SetClientExternalAccountCommand(0, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Neither the client's own profile update nor the staff profile replacement carries the link.</summary>
    [Fact]
    public void ProfileUpdatesCannotCarryTheLink()
    {
        typeof(UpdateMyProfileCommand).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("ExternalAccount", StringComparison.Ordinal));
        typeof(UpdateClientCommand).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("ExternalAccount", StringComparison.Ordinal));
    }

    /// <summary>Reads the stored link of a client.</summary>
    /// <param name="clientId">The client to read.</param>
    /// <returns>The provider and account id, each null when unlinked.</returns>
    private async Task<(string? Provider, string? AccountId)> ReadLinkAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId);
        return (client.ExternalAccountProvider, client.ExternalAccountId);
    }
}
