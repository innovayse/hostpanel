using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary><c>GET /api/admin/fraud/blocklist</c> against Postgres: the type filter, newest first, paging and the authorization boundary.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListBlockListEntriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list filtered by type holds only that type, newest first, and pages.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FiltersByTypeAndPagesNewestFirst()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        foreach (var value in new[] { "one.example", "two.example", "three.example" })
        {
            (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "EmailDomain", value })).EnsureSuccessStatusCode();
        }

        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Country", value = "LY" })).EnsureSuccessStatusCode();

        var first = await admin.GetFromJsonAsync<JsonElement>("/api/admin/fraud/blocklist?type=EmailDomain&page=1&pageSize=2");

        first.GetProperty("totalCount").GetInt32().Should().Be(3);
        var items = first.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items.Select(i => i.GetProperty("type").GetString()).Should().OnlyContain(t => t == "EmailDomain");
        items.Select(i => i.GetProperty("value").GetString()).Should().Equal("three.example", "two.example");
        var second = await admin.GetFromJsonAsync<JsonElement>("/api/admin/fraud/blocklist?type=EmailDomain&page=2&pageSize=2");
        second.GetProperty("items").EnumerateArray().Single().GetProperty("value").GetString().Should().Be("one.example");
    }

    /// <summary>An unknown type is refused when the query is bound, never ignored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnUnknownType()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/admin/fraud/blocklist?type=Nonsense");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Listing the block list is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/fraud/blocklist");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync("/api/admin/fraud/blocklist");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
