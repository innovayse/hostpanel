using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>POST /api/auth/accept-invite</c>: the signed-in caller accepts an invitation token.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AcceptInvitationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Accepting a valid, pending invitation links the signed-in caller to the client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptLinksTheCallerToTheClient()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Host", lastName: "Account");
        var (accepter, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "accept-happy");
        var token = await ClientsTestSeeds.SeedInvitationAsync(factory, clientId, "accepter@example.test");

        var response = await accepter.PostAsJsonAsync("/api/auth/accept-invite", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InvitationAcceptedDto>();
        body!.Success.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reloaded = await db.Clients.Include(c => c.Users).FirstAsync(c => c.Id == clientId);
        reloaded.Users.Should().Contain(u => u.Permissions == ClientPermission.ViewDomains);
    }

    /// <summary>Accepting a token nobody issued answers <c>INVITATION_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptRefusesAnUnknownToken()
    {
        var (accepter, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "accept-unknown");

        var response = await accepter.PostAsJsonAsync("/api/auth/accept-invite", new { token = "no-such-token" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVITATION_NOT_FOUND);
    }

    /// <summary>Accepting an invitation whose client row no longer exists answers <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptRefusesAnOrphanedInvitation()
    {
        var token = await ClientsTestSeeds.SeedInvitationAsync(factory, ClientsTestSeeds.NoSuchClientId, "orphan@example.test");
        var (accepter, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "accept-orphan");

        var response = await accepter.PostAsJsonAsync("/api/auth/accept-invite", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Accepting the same invitation twice answers <c>INVITATION_ALREADY_ACCEPTED</c> the second time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptRefusesAnAlreadyAcceptedInvitation()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Host2", lastName: "Account2");
        var (firstAccepter, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "accept-dupe-first");
        var (secondAccepter, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "accept-dupe-second");
        var token = await ClientsTestSeeds.SeedInvitationAsync(factory, clientId, "accepter2@example.test");
        var first = await firstAccepter.PostAsJsonAsync("/api/auth/accept-invite", new { token });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await secondAccepter.PostAsJsonAsync("/api/auth/accept-invite", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVITATION_ALREADY_ACCEPTED);
    }

    /// <summary>Accepting an invitation is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptRefusesAnonymous()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync("/api/auth/accept-invite", new { token = "whatever" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
