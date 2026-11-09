using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/version</c>: any signed-in staff role (admin or reseller) reads the running
/// version, a caller with only the Client role is refused, and the route is closed to anonymous
/// callers. The body carries the version and nothing about releases.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetInstalledVersionTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    /// <summary>The endpoint path.</summary>
    private const string Url = "/api/admin/version";

    /// <summary>Fixed installed version the fake provider reports.</summary>
    private const string InstalledVersion = "1.2.3";

    /// <summary>Resets the installed version to the baseline so each test starts clean.</summary>
    /// <returns>A completed task.</returns>
    public Task InitializeAsync()
    {
        factory.Services.GetRequiredService<FakeInstalledVersionProvider>().Version = InstalledVersion;
        return Task.CompletedTask;
    }

    /// <summary>Nothing to clean up after a test.</summary>
    /// <returns>A completed task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>An admin gets 200 with only the installed version.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTheInstalledVersionToAnAdminAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("installedVersion").GetString().Should().Be(InstalledVersion);
        body.EnumerateObject().Should().ContainSingle();
    }

    /// <summary>A non-admin staff role (reseller) also gets 200 with the installed version.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTheInstalledVersionToANonAdminStaffRoleAsync()
    {
        var client = await ResellerClientAsync();

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("installedVersion").GetString().Should().Be(InstalledVersion);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "installed-version-forbidden");

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The version endpoint is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Creates a signed-in user who holds only the reseller role.</summary>
    /// <returns>The reseller's authenticated client.</returns>
    private async Task<HttpClient> ResellerClientAsync()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "installed-version-reseller");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByIdAsync(subject.UserId);
            user.Should().NotBeNull();
            // The assertion above fails the test on a missing user; the compiler cannot see that, hence the !.
            (await users.AddToRoleAsync(user!, Roles.Reseller)).Succeeded.Should().BeTrue();
        }

        var http = factory.CreateClient();
        var token = await factory.GetClientTokenAsync(subject.Email, ClientsTestSeeds.TestPassword);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }
}
