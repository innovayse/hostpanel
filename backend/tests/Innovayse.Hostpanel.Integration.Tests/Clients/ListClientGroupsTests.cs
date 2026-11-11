using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>Integration tests for the <c>ListClientGroups</c> use case: <c>GET /api/admin/client-groups</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientGroupsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list holds every group with how many clients belong to it, ordered by name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_AnswersEveryGroupWithItsClientCountByName()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        var later = await ClientGroupTestSeeds.CreateAsync(admin, $"Zeta {token}");
        var earlier = await ClientGroupTestSeeds.CreateAsync(admin, $"Alpha {token}");
        var (firstClient, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (secondClient, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await ClientGroupTestSeeds.AssignAsync(factory, firstClient, earlier);
        await ClientGroupTestSeeds.AssignAsync(factory, secondClient, earlier);

        var all = await admin.GetFromJsonAsync<JsonElement>(ClientGroupTestSeeds.Route);

        var mine = all.EnumerateArray().Where(g => g.GetProperty("name").GetString()!.EndsWith(token, StringComparison.Ordinal)).ToList();
        mine.Select(g => g.GetProperty("id").GetInt32()).Should().Equal(earlier, later);
        mine[0].GetProperty("clientCount").GetInt32().Should().Be(2);
        mine[1].GetProperty("clientCount").GetInt32().Should().Be(0);
    }

    /// <summary>Listing groups is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync(ClientGroupTestSeeds.Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAClient()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "list-groups-forbidden");

        var response = await client.GetAsync(ClientGroupTestSeeds.Route);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
