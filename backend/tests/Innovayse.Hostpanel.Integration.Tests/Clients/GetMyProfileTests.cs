using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>GET /api/clients/me</c>: the authenticated client reads their own profile.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyProfileTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A registered client reads their own profile.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyProfileReturnsTheOwnProfile()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-profile-happy");

        var response = await client.GetAsync("/api/clients/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Portal");
    }

    /// <summary>
    /// The client's own profile carries none of the staff-owned fields: an internal note written
    /// on the record never reaches the client, and neither do the staff switches.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyProfileCarriesNoStaffOwnedField()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-profile-no-staff-fields");
        const string note = "internal note for staff only";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Clients
                .Where(c => c.Id == clientId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.AdminNotes, note));
        }

        var response = await client.GetAsync("/api/clients/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain(note);
        using var body = JsonDocument.Parse(json);
        foreach (var field in new[] { "adminNotes", "status", "overdueNotices", "taxExempt", "separateInvoices", "disableCcProcessing", "allowSso", "billingContact", "userId" })
        {
            body.RootElement.TryGetProperty(field, out _).Should().BeFalse($"the client's own profile must not carry {field}");
        }
    }

    /// <summary>
    /// A caller holding the Client role but no client row (the account was onboarded and then
    /// the client record went away underneath it) answers 404, not 200 of nothing.
    /// </summary>
    /// <remarks>
    /// The seeded admin cannot
    /// stand in for this: <c>/api/clients/me</c> requires the Client role, which the admin account
    /// never holds, so that caller is refused 403 before the handler ever runs. Deleting the row
    /// straight from the database, after registration has already granted the role and created
    /// it, is what leaves the role behind without the record.
    /// </remarks>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyProfileWithNoClientRowRefusesNotFound()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-profile-orphaned");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Clients.Where(c => c.Id == clientId).ExecuteDeleteAsync();
        }

        var response = await client.GetAsync("/api/clients/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }

    /// <summary>Reading the own profile is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyProfileRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/clients/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyProfileRefusesAnAdmin()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/clients/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
