using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>GET /api/admin/servers/options</c>: the unpaged lean server lookup the pickers read. An admin reads every server by
/// name with the facts a picker and the dashboard need and none of the credential or hosting detail, optionally narrowed
/// by module; a module that names nothing is refused with 400; a caller with only the Client role is refused, and the route
/// is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListServerOptionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/admin/servers/options";

    /// <summary>The lookup holds every server, not a page of them, ordered by name, with the facts a picker shows.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsEveryServer_ByName_WithTheFactsOfAPicker()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var ids = new List<int>();
        for (var index = 0; index < 25; index++)
        {
            ids.Add(await ServersTestSeeds.CreateServerAsync(admin, $"{token}-{(char)('a' + index)}", hostname: $"{token}-{index}.example.com", ipAddress: "192.0.2.5"));
        }

        await ServersTestSeeds.RecordConnectionTestAsync(factory, ids[0], isOnline: true, accountsCount: 7);

        var rows = (await ReadAsync(admin, Route)).Where(r => r.GetProperty("name").GetString()!.StartsWith(token, StringComparison.Ordinal)).ToList();

        rows.Select(r => r.GetProperty("id").GetInt32()).Should().Equal(ids);
        var first = rows[0];
        first.GetProperty("hostname").GetString().Should().Be($"{token}-0.example.com");
        first.GetProperty("ipAddress").GetString().Should().Be("192.0.2.5");
        first.GetProperty("isOnline").GetBoolean().Should().BeTrue();
        first.GetProperty("accountsCount").GetInt32().Should().Be(7);
        first.GetProperty("limits").GetProperty("isDisabled").GetBoolean().Should().BeFalse();
        rows[1].GetProperty("isOnline").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>The lookup carries no credential, name server or hosting detail.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_CarriesNoCredentialOrHostingDetail()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        await ServersTestSeeds.CreateServerAsync(admin, $"{token}-lean");

        var row = (await ReadAsync(admin, Route)).Single(r => r.GetProperty("name").GetString() == $"{token}-lean");

        row.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "id", "name", "hostname", "ipAddress", "module", "isOnline", "lastTestedAt", "accountsCount", "limits");
    }

    /// <summary>A server never tested has no test time, and a tested one carries the moment of its last test, as the Servers page shows it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_CarriesTheMomentOfTheLastConnectionTest()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var tested = await ServersTestSeeds.CreateServerAsync(admin, $"{token}-tested");
        await ServersTestSeeds.CreateServerAsync(admin, $"{token}-never");
        await ServersTestSeeds.RecordConnectionTestAsync(factory, tested, isOnline: true, accountsCount: 3);

        var rows = (await ReadAsync(admin, Route)).Where(r => r.GetProperty("name").GetString()!.StartsWith(token, StringComparison.Ordinal)).ToList();

        rows[0].GetProperty("name").GetString().Should().Be($"{token}-never");
        rows[0].GetProperty("lastTestedAt").ValueKind.Should().Be(JsonValueKind.Null);
        rows[1].GetProperty("lastTestedAt").GetDateTimeOffset().Should().Be(TestMoments.Seeded);
    }

    /// <summary>The module filter keeps only the servers that run that module.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ModuleFilterKeepsOnlyThatModule()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var second = await ServersTestSeeds.SeedServerAsync(factory, $"{token}-second", TestPanelModules.Second);
        var cpanel = await ServersTestSeeds.CreateServerAsync(admin, $"{token}-cpanel");

        var onlySecond = await ReadAsync(admin, $"{Route}?module={TestPanelModules.Second}");
        var onlyCPanel = await ReadAsync(admin, $"{Route}?module=CPanel");

        onlySecond.Select(r => r.GetProperty("id").GetInt32()).Should().Contain(second).And.NotContain(cpanel);
        onlyCPanel.Select(r => r.GetProperty("id").GetInt32()).Should().Contain(cpanel).And.NotContain(second);
    }

    /// <summary>A module that is not a well formed id (a space, a slash) is refused with 400 naming the field.</summary>
    /// <param name="filter">The filter sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("module=not%20valid")]
    [InlineData("module=a%2Fb")]
    public async Task List_UnknownModule_IsRefused(string filter)
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain("module");
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "list-server-options-forbidden");

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

    /// <summary>Reads the options an address answers with 200.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="url">The address with its query string.</param>
    /// <returns>The option rows.</returns>
    private static async Task<List<JsonElement>> ReadAsync(HttpClient admin, string url)
    {
        var response = await admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }
}
