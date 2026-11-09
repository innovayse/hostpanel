using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>PATCH /api/clients/me</c>: the authenticated client partially updates their own profile.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateMyProfileTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The own profile route.</summary>
    private const string Route = "/api/clients/me";

    /// <summary>Only the members in the body change; every other stored value is kept.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileChangesOnlyTheProvidedMembers()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "patch-my-profile-partial");
        await SetStoredValuesAsync(clientId, phone: "+37410000000", adminNotes: "keep me", taxExempt: true);

        var response = await client.PatchAsJsonAsync(Route, new
            {
                firstName = "Renamed",
                notifications = new { general = true, invoice = false, support = true, product = true, domain = true, affiliate = true },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("firstName").GetString().Should().Be("Renamed");
        body.GetProperty("notifications").GetProperty("invoice").GetBoolean().Should().BeFalse();
        body.GetProperty("phone").GetString().Should().Be("+37410000000");
        body.GetProperty("notifications").GetProperty("general").GetBoolean().Should().BeTrue();
        var stored = await ReadStoredAsync(clientId);
        stored.AdminNotes.Should().Be("keep me");
        stored.TaxExempt.Should().BeTrue();
    }

    /// <summary>
    /// The sign-in address is not part of the profile update: an email in the body changes nothing,
    /// so the account keeps signing in with the address it has.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileDoesNotChangeTheSignInAddress()
    {
        var (client, _, _, email) = await ClientsTestSeeds.RegisterClientAsync(factory, "patch-my-profile-email");
        var attempted = $"patched-{Guid.NewGuid():N}@example.test";

        var response = await client.PatchAsJsonAsync(Route, new { email = attempted, firstName = "Kept" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = attempted, password = ClientsTestSeeds.TestPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = ClientsTestSeeds.TestPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Staff-owned members in the body are not part of the command and change nothing: currency,
    /// admin notes, billing settings and status stay as stored.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileIgnoresStaffOwnedMembers()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "patch-my-profile-staff");
        var before = await client.GetFromJsonAsync<JsonElement>(Route);
        var storedBefore = await ReadStoredAsync(clientId);

        var response = await client.PatchAsJsonAsync(
            Route,
            new { lastName = "Changed", currency = "JPY", adminNotes = "self-written", taxExempt = true, status = "Closed" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("lastName").GetString().Should().Be("Changed");
        body.GetProperty("currency").GetString().Should().Be(before.GetProperty("currency").GetString());
        var storedAfter = await ReadStoredAsync(clientId);
        storedAfter.AdminNotes.Should().Be(storedBefore.AdminNotes);
        storedAfter.TaxExempt.Should().Be(storedBefore.TaxExempt);
        storedAfter.Status.Should().Be(storedBefore.Status);
    }

    /// <summary>An address that names only some lines changes those lines and keeps the stored ones.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileKeepsTheAddressLinesTheBodyLeavesOut()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "patch-my-profile-address");
        (await client.PatchAsJsonAsync(Route, new { address = new { city = "Gyumri" } })).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PatchAsJsonAsync(Route, new { address = new { street = "1 Main Street" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var address = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("address");
        address.GetProperty("street").GetString().Should().Be("1 Main Street");
        address.GetProperty("city").GetString().Should().Be("Gyumri");
    }

    /// <summary>An empty body is a valid no-op and answers the profile unchanged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileWithAnEmptyBodyChangesNothing()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "patch-my-profile-empty");
        var before = await client.GetFromJsonAsync<JsonElement>(Route);

        var response = await client.PatchAsJsonAsync(Route, new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("firstName").GetString().Should().Be(before.GetProperty("firstName").GetString());
        body.GetProperty("lastName").GetString().Should().Be(before.GetProperty("lastName").GetString());
    }

    /// <summary>A provided first name that is blank is refused by the shared field rules with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileRefusesABlankFirstName()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "patch-my-profile-blank");

        var response = await client.PatchAsJsonAsync(Route, new { firstName = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with the Client role but no client row owns no account and is linked to none, so the permission gate refuses before any write with 403 <c>CLIENT_PERMISSION_DENIED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileWithNoClientRowIsRefusedByThePermissionGate()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "patch-my-profile-orphaned");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Clients.Where(c => c.Id == clientId).ExecuteDeleteAsync();
        }

        var response = await client.PatchAsJsonAsync(Route, new { firstName = "Nobody" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileRefusesAnonymous()
    {
        var response = await factory.CreateClient().PatchAsJsonAsync(Route, new { firstName = "Anon" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyProfileRefusesAnAdmin()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PatchAsJsonAsync(Route, new { firstName = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Writes stored values the client cannot set through this route, so a test can see they survive.</summary>
    /// <param name="clientId">The client row to write.</param>
    /// <param name="phone">The phone number to store.</param>
    /// <param name="adminNotes">The admin notes to store.</param>
    /// <param name="taxExempt">The tax exempt flag to store.</param>
    /// <returns>A task that completes once the row is written.</returns>
    private async Task SetStoredValuesAsync(int clientId, string phone, string adminNotes, bool taxExempt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Clients
            .Where(c => c.Id == clientId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Phone, phone)
                .SetProperty(c => c.AdminNotes, adminNotes)
                .SetProperty(c => c.TaxExempt, taxExempt));
    }

    /// <summary>
    /// Reads the stored client row, since the staff-owned fields are not part of the client's own
    /// profile and can only be checked where they are kept.
    /// </summary>
    /// <param name="clientId">The client row to read.</param>
    /// <returns>The stored client.</returns>
    private async Task<Client> ReadStoredAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId);
    }
}
