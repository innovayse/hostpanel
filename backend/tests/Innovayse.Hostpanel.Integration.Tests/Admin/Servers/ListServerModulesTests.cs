using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>GET /api/admin/servers/modules</c>: the control panel modules the server form offers. An admin reads the host's own modules, each
/// built in; a caller with only the Client role is refused, and the route is closed to a caller with no credential. The plugin
/// modules the list adds are covered by <c>ServerModuleCatalogTests</c>, which drives the catalog the route reads.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListServerModulesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/admin/servers/modules";

    /// <summary>An admin reads every module the host knows, with its id, name and source.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsTheHostsModulesAsync()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("id").GetString()).Should().Contain(ServerModules.Known);
        rows.Single(r => r.GetProperty("id").GetString() == ServerModules.CPanel).GetProperty("source").GetString().Should().Be("BuiltIn");
        rows.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.GetProperty("name").GetString()));
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "list-server-modules-forbidden");

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
