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

    /// <summary>The search finds a client by its company, which is not part of the name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsAClientByCompany()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Company", lastName: "Search", companyName: $"Acme{token} Hosting");

        var ids = await ListIdsAsync($"page=1&pageSize=100&search={Uri.EscapeDataString($"ACME{token}")}");

        ids.Should().Contain(clientId);
    }

    /// <summary>A search that names no one narrows the list to nothing instead of ignoring the text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWithNoMatchReturnsAnEmptyPage()
    {
        await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Seen", lastName: "Somewhere");

        var ids = await ListIdsAsync($"page=1&pageSize=100&search={Uri.EscapeDataString($"nobody-{Guid.NewGuid():N}")}");

        ids.Should().BeEmpty();
    }

    /// <summary>The search and a filter narrow together: only a client that matches both is listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchAndPhoneFilterNarrowTogether()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (withPhone, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Both", lastName: $"Narrow{token}", phone: "+1 555 0101");
        var (withoutPhone, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Both", lastName: $"Narrow{token}", phone: "+1 555 0202");

        var ids = await ListIdsAsync($"page=1&pageSize=100&search=Narrow{token}&phone={Uri.EscapeDataString("555 0101")}");

        ids.Should().Contain(withPhone).And.NotContain(withoutPhone);
    }

    /// <summary>The total count and the page window follow the search, so the pager is right for the narrowed list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchPagesOverTheNarrowedList()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
        {
            await ClientsTestSeeds.SeedClientAsync(factory, firstName: $"Paged{i}", lastName: $"Window{token}");
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"/api/clients?page=2&pageSize=2&search=Window{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
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

    /// <summary>The refusal of a page size names the limits, so the caller learns them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PageSizeRefusalNamesTheLimits()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/clients?page=1&pageSize=5000");

        (await response.Content.ReadAsStringAsync()).Should().Contain("between 1 and 100");
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
