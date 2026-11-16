using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>GET /api/admin/servers</c>: the paged server list. An admin reads one page by name, narrowed by a search over the
/// name, host name and IP address, a module and a last known state; a value out of range or a filter that names nothing
/// is refused with 400 on its field; a caller with only the Client role is refused, and the route is closed to a caller
/// with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListServersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/admin/servers";

    /// <summary>The servers one seeded mix holds, by the role each plays in the tests.</summary>
    /// <param name="Token">The text every seeded name and host name carries, so a search scopes a request to this mix.</param>
    /// <param name="Online">A Plesk server whose last test succeeded; first by name.</param>
    /// <param name="Offline">A Cwp7 server whose last test failed; second by name.</param>
    /// <param name="Untested">A Cwp7 server nobody has tested; last by name.</param>
    private sealed record ServerMix(string Token, int Online, int Offline, int Untested);

    /// <summary>A page lists the servers by name and counts every match.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsTheServers_ByName()
    {
        var mix = await SeedMixAsync();
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var (ids, totalCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}");

        ids.Should().Equal(mix.Online, mix.Offline, mix.Untested);
        totalCount.Should().Be(3);
    }

    /// <summary>A row carries the connection state and the name of its group, looked up for the page.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RowCarriesItsStateAndGroupName()
    {
        var mix = await SeedMixAsync();
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var groupName = $"group-{mix.Token}";
        await ServersTestSeeds.CreateServerGroupAsync(admin, groupName, mix.Offline);

        var page = await PagedListReads.PageAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}");

        var rows = page.GetProperty("items").EnumerateArray().ToList();
        rows[0].GetProperty("isOnline").GetBoolean().Should().BeTrue();
        rows[0].GetProperty("serverGroupName").ValueKind.Should().Be(JsonValueKind.Null);
        rows[1].GetProperty("isOnline").GetBoolean().Should().BeFalse();
        rows[1].GetProperty("serverGroupName").GetString().Should().Be(groupName);
        rows[2].GetProperty("isOnline").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>The search matches the name, the host name and the IP address, in any letter case, as a substring.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchMatchesNameHostnameAndIpAddress()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var byName = await ServersTestSeeds.CreateServerAsync(admin, $"{token}-name", hostname: "plain-one.example.com");
        var byHostname = await ServersTestSeeds.CreateServerAsync(admin, "plain-two", hostname: $"{token}-host.example.com");
        var byIp = await ServersTestSeeds.CreateServerAsync(admin, "plain-three", hostname: "plain-three.example.com", ipAddress: "192.0.2.77");

        var (nameIds, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={token.ToUpperInvariant()}-NAME");
        var (hostIds, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={token}-host");
        var (ipIds, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search=192.0.2.77");

        nameIds.Should().Equal(byName);
        hostIds.Should().Equal(byHostname);
        ipIds.Should().Equal(byIp);
    }

    /// <summary>A <c>%</c> or <c>_</c> typed in the search matches itself, not any text or any single character.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchWildcardsMatchThemselves()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var literal = await ServersTestSeeds.CreateServerAsync(admin, $"{token} 50% off_sale", hostname: $"{token}-literal.example.com");
        await ServersTestSeeds.CreateServerAsync(admin, $"{token} 50 percent offXsale", hostname: $"{token}-other.example.com");

        var (percent, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={Uri.EscapeDataString($"{token} 50% off_")}");
        var (anyText, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={Uri.EscapeDataString($"{token}%sale")}");
        var (anyChar, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={Uri.EscapeDataString($"{token} 50 percent offX_ale")}");

        percent.Should().Equal(literal);
        anyText.Should().BeEmpty();
        anyChar.Should().BeEmpty();
    }

    /// <summary>The module filter keeps only the servers that run that module.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ModuleFilterKeepsOnlyThatModule()
    {
        var mix = await SeedMixAsync();
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var (plesk, pleskCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}&module=Plesk");
        var (cwp7, cwp7Count) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}&module=Cwp7");
        var (cpanel, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}&module=CPanel");

        plesk.Should().Equal(mix.Online);
        pleskCount.Should().Be(1);
        cwp7.Should().Equal(mix.Offline, mix.Untested);
        cwp7Count.Should().Be(2);
        cpanel.Should().BeEmpty();
    }

    /// <summary>The state filter keeps only the servers in that last known state, an untested server being a state of its own.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_StateFilterKeepsOnlyThatState()
    {
        var mix = await SeedMixAsync();
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var (online, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}&state=Online");
        var (offline, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}&state=offline");
        var (untested, untestedCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}&state=Untested");
        var (both, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&search={mix.Token}&module=Cwp7&state=Untested");

        online.Should().Equal(mix.Online);
        offline.Should().Equal(mix.Offline);
        untested.Should().Equal(mix.Untested);
        untestedCount.Should().Be(1);
        both.Should().Equal(mix.Untested);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, by name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IsPagedAfterTheFilters()
    {
        var mix = await SeedMixAsync();
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var (first, firstCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=2&search={mix.Token}");
        var (second, secondCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=2&pageSize=2&search={mix.Token}");
        var (beyond, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=3&pageSize=2&search={mix.Token}");

        first.Should().Equal(mix.Online, mix.Offline);
        second.Should().Equal(mix.Untested);
        firstCount.Should().Be(3);
        secondCount.Should().Be(3);
        beyond.Should().BeEmpty();
    }

    /// <summary>A page, a page size or a search outside its limits is refused with 400 on that field rather than cut or defaulted.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <param name="field">The field the refusal must name, in lower case.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    public async Task List_PagingOutsideTheLimits_IsRefused(string queryString, string field)
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchOverTheLimit_IsRefused()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain("search");
    }

    /// <summary>
    /// A module or a state that names nothing, by name or by number, is refused with 400 on its field rather than listing every
    /// server. The model binder refuses an unknown name before the validator runs and the validator refuses an unknown number.
    /// </summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name, in lower case.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("module=Nonsense", "module")]
    [InlineData("module=99", "module")]
    [InlineData("state=Nonsense", "state")]
    [InlineData("state=99", "state")]
    public async Task List_UnknownModuleOrState_IsRefused(string filter, string field)
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "list-servers-forbidden");

        var response = await client.GetAsync($"{Route}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync($"{Route}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Seeds an online Plesk server, an offline Cwp7 server and an untested Cwp7 server, named so they sort in that order.</summary>
    /// <returns>The mix.</returns>
    private async Task<ServerMix> SeedMixAsync()
    {
        var admin = await ServersTestSeeds.AdminClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var online = await ServersTestSeeds.CreateServerAsync(admin, $"{token}-a", ServerModule.Plesk);
        var offline = await ServersTestSeeds.CreateServerAsync(admin, $"{token}-b");
        var untested = await ServersTestSeeds.CreateServerAsync(admin, $"{token}-c");
        await ServersTestSeeds.RecordConnectionTestAsync(factory, online, isOnline: true, accountsCount: 4);
        await ServersTestSeeds.RecordConnectionTestAsync(factory, offline, isOnline: false, accountsCount: null);
        return new ServerMix(token, online, offline, untested);
    }
}
