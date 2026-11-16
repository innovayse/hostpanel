using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary>
/// <c>GET /api/admin/fraud/blocklist</c> against Postgres: the search on the value and the note (a <c>%</c> or <c>_</c>
/// in it matches itself), the type filter, newest first, paging, the refusal of a bad paging window, search or type, and
/// the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListBlockListEntriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list filtered by type holds only that type, newest first, and pages.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FiltersByTypeAndPagesNewestFirst()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        foreach (var value in new[] { $"one-{token}.example", $"two-{token}.example", $"three-{token}.example" })
        {
            (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "EmailDomain", value })).EnsureSuccessStatusCode();
        }

        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "Country", value = "LY", note = token })).EnsureSuccessStatusCode();

        var first = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/fraud/blocklist?type=EmailDomain&search={token}&page=1&pageSize=2");

        first.GetProperty("totalCount").GetInt32().Should().Be(3);
        var items = first.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items.Select(i => i.GetProperty("type").GetString()).Should().OnlyContain(t => t == "EmailDomain");
        items.Select(i => i.GetProperty("value").GetString()).Should().Equal($"three-{token}.example", $"two-{token}.example");
        var second = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/fraud/blocklist?type=EmailDomain&search={token}&page=2&pageSize=2");
        second.GetProperty("items").EnumerateArray().Single().GetProperty("value").GetString().Should().Be($"one-{token}.example");
    }

    /// <summary>The search finds an entry by a part of its value or of its note, in any letter case, and the type filter narrows it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchMatchesTheValueAndTheNote()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        await AddAsync(admin, "EmailDomain", $"{token}-domain.example", null);
        await AddAsync(admin, "Country", "SY", $"seen from {token}");
        await AddAsync(admin, "Country", "KP", "unrelated");

        var byToken = await ValuesAsync(admin, $"search={token.ToUpperInvariant()}");
        var byNote = await ValuesAsync(admin, "search=SEEN+FROM");
        var countriesOnly = await ValuesAsync(admin, $"search={token}&type=Country");

        byToken.Should().Equal("SY", $"{token}-domain.example");
        byNote.Should().Contain("SY").And.NotContain("KP");
        countriesOnly.Should().Equal("SY");
    }

    /// <summary>A <c>%</c> or <c>_</c> in the search is text to find, not a wildcard that matches every entry.</summary>
    /// <param name="wildcard">The character typed after the token.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task SearchTreatsAWildcardAsText(string wildcard)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        await AddAsync(admin, "EmailDomain", $"{token}-wild.example", null);

        var found = await ValuesAsync(admin, $"search={Uri.EscapeDataString(token + wildcard)}");

        found.Should().BeEmpty();
    }

    /// <summary>A missing page, a page below one or a page size outside the limit is refused with 400 on the field, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=1&pageSize=101", "pageSize")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/fraud/blocklist?{queryString}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/fraud/blocklist?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "search");
    }

    /// <summary>A type that names nothing, by name or by number, is refused with 400 on the field, never ignored.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("type=Nonsense")]
    [InlineData("type=99")]
    public async Task RefusesAnUnknownType(string filter)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/fraud/blocklist?page=1&pageSize=20&{filter}");

        // The model binder refuses a value that is no member before the validator runs, so the check is that the field is named.
        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "type");
    }

    /// <summary>Listing the block list is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/fraud/blocklist?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync("/api/admin/fraud/blocklist?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Adds one entry through the real endpoint.</summary>
    /// <param name="admin">The admin caller.</param>
    /// <param name="type">What the value names.</param>
    /// <param name="value">The value to block.</param>
    /// <param name="note">Why it is blocked, or <see langword="null"/>.</param>
    /// <returns>A task that completes once the entry is added.</returns>
    private static async Task AddAsync(HttpClient admin, string type, string value, string? note)
    {
        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type, value, note })).EnsureSuccessStatusCode();
    }

    /// <summary>Reads the values of the entries a filter returns.</summary>
    /// <param name="admin">The admin caller.</param>
    /// <param name="filter">The query string filter.</param>
    /// <returns>The values, newest first.</returns>
    private static async Task<List<string?>> ValuesAsync(HttpClient admin, string filter)
    {
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/fraud/blocklist?page=1&pageSize=100&{filter}");
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("value").GetString()).ToList();
    }
}
