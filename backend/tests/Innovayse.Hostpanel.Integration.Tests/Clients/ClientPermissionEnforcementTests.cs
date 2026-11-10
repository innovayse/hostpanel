using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// The client portal enforces the permissions of a linked user: an action that needs a
/// <see cref="ClientPermission"/> refuses a user who was not granted it with 403
/// <c>CLIENT_PERMISSION_DENIED</c>, admits one who was, and never refuses the account owner.
/// </summary>
/// <remarks>
/// <para>
/// The user under test owns no client record and is linked to a seeded account through the real
/// invitation flow, so the only thing that can admit them is the stored grant. "Admits" is asserted as
/// "is not refused for permission": the use cases behind the portal still resolve the caller's own
/// record, so a linked user with no record of their own is answered by the use case (a 404 or a 400),
/// which is not what these tests are about.
/// </para>
/// <para>
/// Each case is an HTTP request of a different permission, so a controller action whose declaration
/// is wrong or missing fails here with its own name in the output.
/// </para>
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientPermissionEnforcementTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>One portal request per permission, covering every flag a portal endpoint uses.</summary>
    /// <returns>Rows of HTTP method, path, and the permission the path needs.</returns>
    public static TheoryData<string, string, ClientPermission> Requests() => new()
    {
        { "GET", "/api/me/invoices", ClientPermission.ViewPayInvoices },
        { "GET", "/api/me/invoices/1/pdf", ClientPermission.ViewPayInvoices },
        { "GET", "/api/me/stored-payment-methods", ClientPermission.ViewPayInvoices },
        { "GET", "/api/me/tickets", ClientPermission.ViewOpenSupportTickets },
        { "POST", "/api/me/tickets", ClientPermission.ViewOpenSupportTickets },
        { "GET", "/api/me/emails", ClientPermission.ViewEmails },
        { "GET", "/api/me/services", ClientPermission.ViewProductsServices },
        { "POST", "/api/me/services", ClientPermission.PlaceNewOrders },
        { "POST", "/api/me/services/1/cancel", ClientPermission.PlaceNewOrders },
        { "GET", "/api/me/services/1/cpanel-sso", ClientPermission.AllowSingleSignOn },
        { "POST", "/api/me/services/1/change-password", ClientPermission.ViewModifyPasswords },
        { "GET", "/api/me/domains", ClientPermission.ViewDomains },
        { "GET", "/api/me/domains/1/nameservers", ClientPermission.ViewDomains },
        { "PUT", "/api/me/domains/1/auto-renew", ClientPermission.ManageDomainSettings },
        { "POST", "/api/me/domains/1/dns", ClientPermission.ManageDomainSettings },
        { "POST", "/api/me/domains/1/renew", ClientPermission.PlaceNewOrders },
        { "GET", "/api/clients/me/contacts", ClientPermission.ViewManageContacts },
        { "POST", "/api/clients/me/contacts", ClientPermission.ViewManageContacts },
        { "PATCH", "/api/clients/me", ClientPermission.ModifyMasterProfile },
        { "GET", "/api/me/users", ClientPermission.All },
        { "POST", "/api/me/users/invite", ClientPermission.All },
    };

    /// <summary>Sends an empty-bodied request of the given method; a bodiless request still reaches the filter.</summary>
    /// <param name="client">The caller.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "GET")
        {
            request.Content = JsonContent.Create(new { });
        }

        return client.SendAsync(request);
    }

    /// <summary>A linked user granted every permission except the one the action needs is refused.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    /// <param name="needs">The permission the path needs.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [MemberData(nameof(Requests))]
    public async Task ALinkedUserWithoutThePermission_IsRefused(string method, string path, ClientPermission needs)
    {
        // Everything but one flag: if the action checked "any permission at all" this would pass it.
        var granted = needs == ClientPermission.All ? ClientPermission.All & ~ClientPermission.ModifyMasterProfile : ClientPermission.All & ~needs;
        var user = await ClientsTestSeeds.LinkedUserAsync(factory, "perm-denied", granted);

        var response = await SendAsync(user, method, path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path} needs {needs}");
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A linked user granted exactly the permission the action needs is not refused for permission.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    /// <param name="needs">The permission the path needs.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [MemberData(nameof(Requests))]
    public async Task ALinkedUserWithThePermission_IsNotRefusedForIt(string method, string path, ClientPermission needs)
    {
        var user = await ClientsTestSeeds.LinkedUserAsync(factory, "perm-granted", needs);

        var response = await SendAsync(user, method, path);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED, $"{method} {path} with {needs}");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, $"{method} {path} with {needs}");
    }

    /// <summary>The account owner is never refused on any action, whatever the table says it needs.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path.</param>
    /// <param name="needs">The permission the path needs.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [MemberData(nameof(Requests))]
    public async Task TheAccountOwner_IsNeverRefusedForPermission(string method, string path, ClientPermission needs)
    {
        var (owner, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "perm-owner");

        var response = await SendAsync(owner, method, path);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED, $"{method} {path} (needs {needs})");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, $"{method} {path} (needs {needs})");
    }

    /// <summary>An owner reads their own invoices: the gate admits them and the use case answers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheOwner_ReadsTheirOwnInvoices()
    {
        var (owner, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "perm-owner-invoices");

        var response = await owner.GetAsync("/api/me/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user who holds the client role but is on no account at all is refused: no link means no permission.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AClientRoleUserOnNoAccount_IsRefused()
    {
        var (user, userId, _) = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "perm-no-account");
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>().AddAsync(userId, Role.Client, CancellationToken.None);
        }

        var response = await user.GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A stored bit that no permission defines grants nothing, so it cannot open an action by accident.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownStoredBit_GrantsNothing()
    {
        var user = await ClientsTestSeeds.LinkedUserAsync(factory, "perm-unknown-bit", (ClientPermission)0x4000);

        var response = await user.GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A user linked to two accounts holds only what both links grant: nothing says which account is meant.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AUserLinkedToTwoAccounts_HoldsOnlyWhatBothGrant()
    {
        var (user, _, email) = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "perm-two-links");
        var (first, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (second, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await ClientsTestSeeds.LinkUserAsync(factory, user, email, first, ClientPermission.ViewEmails | ClientPermission.ViewDomains);
        await ClientsTestSeeds.LinkUserAsync(factory, user, email, second, ClientPermission.ViewDomains);

        var emails = await user.GetAsync("/api/me/emails");
        var domains = await user.GetAsync("/api/me/domains");

        emails.StatusCode.Should().Be(HttpStatusCode.Forbidden, "only one of the two links grants ViewEmails");
        domains.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "both links grant ViewDomains");
    }

    /// <summary>
    /// Linked to another account with a narrow grant, a user who owns a client record keeps the full access
    /// of the account they own: the owner bypass is decided by the record, not by the link.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnOwnerLinkedElsewhereWithANarrowGrant_KeepsTheirOwnAccess()
    {
        var (owner, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "perm-owner-linked");
        var email = (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("email").GetString()!;
        var (other, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await ClientsTestSeeds.LinkUserAsync(factory, owner, email, other, ClientPermission.ViewDomains);

        var response = await owner.GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Identity comes from the credential: a request that names the owner's subject in its query, a header and a body
    /// is still judged as the linked user it was signed in as.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AUserNamingSomeoneElseInTheRequest_IsStillJudgedAsThemselves()
    {
        var (owner, _, ownerId) = await ClientsTestSeeds.RegisterClientAsync(factory, "perm-impersonate");
        var user = await ClientsTestSeeds.LinkedUserAsync(factory, "perm-impersonator", ClientPermission.ViewDomains);
        user.DefaultRequestHeaders.Add("X-User-Id", ownerId);

        var response = await user.GetAsync($"/api/me/emails?userId={ownerId}&clientId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        (await owner.GetAsync("/api/me/emails")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The refusal is the standard error body, in the caller's language, with a sentence and not the bare code.</summary>
    /// <param name="language">The <c>X-Language</c> value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task TheRefusal_IsTheStandardErrorBodyInTheCallersLanguage(string language)
    {
        var user = await ClientsTestSeeds.LinkedUserAsync(factory, "perm-body", ClientPermission.ViewDomains);
        user.DefaultRequestHeaders.Add("X-Language", language);

        var response = await user.GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        body.GetProperty("status").GetInt32().Should().Be(403);
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace().And.NotBe(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>The account endpoint reports the caller's permissions: all for the owner, the grant for a linked user, none without an account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheAccountEndpoint_ReportsThePermissionsTheCallerHolds()
    {
        var (owner, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "perm-me-owner");
        var granted = ClientPermission.ViewDomains | ClientPermission.ViewEmails;
        var linked = await ClientsTestSeeds.LinkedUserAsync(factory, "perm-me-linked", granted);
        var (stranger, _, _) = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "perm-me-none");

        var ownerBody = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var linkedBody = await linked.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var strangerBody = await stranger.GetFromJsonAsync<JsonElement>("/api/auth/me");

        ownerBody.GetProperty("clientPermissions").GetInt32().Should().Be((int)ClientPermission.All);
        linkedBody.GetProperty("clientPermissions").GetInt32().Should().Be((int)granted);
        strangerBody.GetProperty("clientPermissions").GetInt32().Should().Be((int)ClientPermission.None);
    }

    /// <summary>The profile the portal opens with is open to every user of the account, whatever they were granted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheProfile_IsNotGatedByAPermission()
    {
        var user = await ClientsTestSeeds.LinkedUserAsync(factory, "perm-profile", ClientPermission.ViewEmails);

        var response = await user.GetAsync("/api/clients/me");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>The route is not for anonymous callers; the permission filter is never what answers them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnAnonymousCaller_IsAskedToSignInNotToHoldAPermission()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
