using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>POST /api/me/users/invite</c>: a client invites someone to their own account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InviteMyUserTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Inviting a fresh address with a chosen permission list succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteSendsTheInvitation()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-happy");

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = new[] { "ViewManageContacts", "ViewPayInvoices" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An invitation sent from the portal leaves an activity line on the caller's own client, free of the invited address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteWritesAnAuditLine()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-audit");

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "All",
            permissions = Array.Empty<string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var lines = await db.ActivityLogs.AsNoTracking().Where(l => l.ClientId == clientId).ToListAsync();
        var line = lines.Should().ContainSingle(l => l.Description.StartsWith("Invited User", StringComparison.Ordinal)).Subject;
        line.Description.Should().NotContain("@");
    }

    /// <summary>Inviting with every permission sends the mode and an empty list, and succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteWithAllPermissionsSendsTheInvitation()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-all");

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "All",
            permissions = Array.Empty<string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Choosing permissions but listing none is a validation error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesAnEmptyChosenList()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-empty");

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = Array.Empty<string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A list entry that combines permissions is a validation error, not a wider grant.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesACombinedListEntry()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-combined");

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = new[] { "All" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>Granting every permission while also listing some is a validation error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesAListWithAllMode()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-all-list");

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "All",
            permissions = new[] { "ViewDomains" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>Inviting the caller's own address answers <c>CANNOT_INVITE_ACCOUNT_OWNER</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesTheCallersOwnAddress()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-self");
        var me = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/clients/me");
        var ownEmail = me.GetProperty("email").GetString();

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = ownEmail,
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = new[] { "ViewManageContacts" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CANNOT_INVITE_ACCOUNT_OWNER);
    }

    /// <summary>Inviting the same address twice answers <c>INVITATION_ALREADY_PENDING</c> the second time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesADuplicatePendingInvitation()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-invite-dupe");
        var inviteeEmail = $"invitee-{Guid.NewGuid():N}@example.test";
        var first = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = inviteeEmail,
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = new[] { "ViewManageContacts" },
        });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = inviteeEmail,
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = new[] { "ViewManageContacts" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVITATION_ALREADY_PENDING);
    }

    /// <summary>Inviting someone to the caller's own account is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/users/invite", new
        {
            email = "invitee@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = new[] { "ViewManageContacts" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesAnAdmin()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/me/users/invite", new
        {
            email = "invitee@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissionMode = "Choose",
            permissions = new[] { "ViewManageContacts" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
