using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>GET /api/clients</c>: the paginated, filterable admin client list.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A seeded client shows up on the default, unfiltered first page.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesASeededClient()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Margaret", lastName: "Hamilton");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/clients?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32())
            .Should().Contain(clientId);
    }

    /// <summary>Listing clients is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/clients");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAClient()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "list-forbidden");

        var response = await client.GetAsync("/api/clients");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The ids on the page the admin list answered for the given query string.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <returns>The client ids of the page, in the order the list gave them.</returns>
    private async Task<List<int>> ListIdsAsync(string queryString)
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"/api/clients?{queryString}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
    }

    /// <summary>The search finds a client by the full name read as "First Last", in any letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsAClientByFullName()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: $"Grace{token}", lastName: $"Hopper{token}");

        var ids = await ListIdsAsync($"page=1&pageSize=100&search={Uri.EscapeDataString($"gRaCe{token} hOpPeR{token}".ToUpperInvariant())}");

        ids.Should().Contain(clientId);
    }

    /// <summary>A whole-number search finds the client with that id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsAClientById()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "ById", lastName: Guid.NewGuid().ToString("N")[..8]);

        var ids = await ListIdsAsync($"page=1&pageSize=100&search={clientId}");

        ids.Should().Contain(clientId);
    }

    /// <summary>A search wildcard typed by a person matches itself and nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchTreatsWildcardsAsLiteralText()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (plainId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: $"Plain{token}", lastName: "Wildcard");

        var ids = await ListIdsAsync($"page=1&pageSize=100&search={Uri.EscapeDataString($"Plain{token}%")}");

        ids.Should().NotContain(plainId);
    }

    /// <summary>Equal names are ordered by id, so pages never repeat or skip a client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListOrdersEqualNamesById()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (first, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Same", lastName: $"Name{token}");
        var (second, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Same", lastName: $"Name{token}");

        var ids = await ListIdsAsync($"page=1&pageSize=100&search={Uri.EscapeDataString($"Name{token}")}");

        ids.Should().Equal(first, second);
    }

    /// <summary>The status filter keeps only clients in that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterKeepsOnlyThatStatus()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (activeId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Status", lastName: $"Filter{token}");

        (await ListIdsAsync($"page=1&pageSize=100&search=Filter{token}&status=Closed")).Should().NotContain(activeId);
        (await ListIdsAsync($"page=1&pageSize=100&search=Filter{token}&status=Active")).Should().Contain(activeId);
    }

    /// <summary>A page size above the limit is refused with 400, not silently reduced.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAPageSizeAboveTheLimit()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/clients?page=1&pageSize=5000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A page number below 1 is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAPageBelowOne()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/clients?page=0&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An unknown status, by name or by number, is refused with 400.</summary>
    /// <param name="status">The status value sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Nonsense")]
    [InlineData("99")]
    public async Task ListRefusesAnUnknownStatus(string status)
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients?page=1&pageSize=20&status={status}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A search, email or phone filter longer than the limit is refused with 400.</summary>
    /// <param name="filter">The query-string key of the filter.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("search")]
    [InlineData("email")]
    [InlineData("phone")]
    public async Task ListRefusesAnOverlongFilter(string filter)
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients?page=1&pageSize=20&{filter}={new string('a', 201)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
