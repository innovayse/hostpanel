using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>
/// Integration tests for the <c>ListTaxRules</c> use case: <c>GET /api/admin/tax-rules</c>, the paged list that searches the
/// name, country and region and filters by the enabled state.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTaxRulesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The tax rules one seeded mix holds, by the role each plays in the tests.</summary>
    /// <param name="Token">The text every seeded name carries, so a search scopes a request to this mix.</param>
    /// <param name="Wide">An enabled country-wide rule of the first country.</param>
    /// <param name="Region">A disabled rule limited to a region of the first country.</param>
    /// <param name="Other">An enabled country-wide rule of a later country.</param>
    private sealed record RuleMix(string Token, int Wide, int Region, int Other);

    /// <summary>The list holds the rules of a page, each country's country-wide rule before its regions.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsTheRulesCountryWideFirst()
    {
        var mix = await SeedMixAsync("QA", "QB");

        var (ids, totalCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}");

        ids.Should().Equal(mix.Wide, mix.Region, mix.Other);
        totalCount.Should().Be(3);
    }

    /// <summary>The search matches the name in any letter case, as a substring, and leaves other rules out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchMatchesTheNameCaseInsensitively()
    {
        var mix = await SeedMixAsync("QC", "QD");

        var (ids, totalCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token.ToUpperInvariant()}%20OTHER");

        ids.Should().Equal(mix.Other);
        totalCount.Should().Be(1);
    }

    /// <summary>The search also matches the region and the country code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchMatchesTheRegionAndTheCountry()
    {
        var mix = await SeedMixAsync("QE", "QF");

        var (byRegion, byRegionCount) = await ListAsync($"page=1&pageSize=100&search=gauteng{mix.Token}");
        var (byCountry, byCountryCount) = await ListAsync("page=1&pageSize=100&search=qf");

        byRegion.Should().Equal(mix.Region);
        byRegionCount.Should().Be(1);
        byCountry.Should().Equal(mix.Other);
        byCountryCount.Should().Be(1);
    }

    /// <summary>A <c>%</c> or <c>_</c> typed in the search matches itself, not any text or any single character.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchWildcardsMatchThemselves()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var literal = await CreateAsync(admin, $"{token} 5% a_b", "QK", null, true);
        await CreateAsync(admin, $"{token} 5X aXb", "QK", "North", true);

        var (percent, percentCount) = await ListAsync($"page=1&pageSize=100&search={token}%205%25%20a_b");
        var (anyText, anyTextCount) = await ListAsync($"page=1&pageSize=100&search={token}%25aXb");
        var (anyChar, anyCharCount) = await ListAsync($"page=1&pageSize=100&search={token}%205X%20a_b");

        percent.Should().Equal(literal);
        percentCount.Should().Be(1);
        anyText.Should().BeEmpty();
        anyTextCount.Should().Be(0);
        anyChar.Should().BeEmpty();
        anyCharCount.Should().Be(0);
    }

    /// <summary>The enabled filter keeps the rules in use or the ones switched off, and no filter keeps both.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_EnabledFilterKeepsOnlyThatState()
    {
        var mix = await SeedMixAsync("QG", "QH");

        var (enabled, enabledCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&enabled=true");
        var (disabled, disabledCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&enabled=false");

        enabled.Should().Equal(mix.Wide, mix.Other);
        enabledCount.Should().Be(2);
        disabled.Should().Equal(mix.Region);
        disabledCount.Should().Be(1);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, in order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IsPagedAfterTheFilters()
    {
        var mix = await SeedMixAsync("QI", "QJ");

        var (first, firstCount) = await ListAsync($"page=1&pageSize=2&search={mix.Token}");
        var (second, secondCount) = await ListAsync($"page=2&pageSize=2&search={mix.Token}");
        var (beyond, _) = await ListAsync($"page=3&pageSize=2&search={mix.Token}");

        first.Should().Equal(mix.Wide, mix.Region);
        second.Should().Equal(mix.Other);
        firstCount.Should().Be(3);
        secondCount.Should().Be(3);
        beyond.Should().BeEmpty();
    }

    /// <summary>A page, a page size or a search outside its limits is refused with 400 on that field rather than cut or defaulted.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=1&pageSize=101", "pageSize")]
    public async Task List_PagingOutsideTheLimits_IsRefused(string queryString, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{TaxRuleTestSeeds.Route}?{queryString}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchOverTheLimit_IsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync(
            $"{TaxRuleTestSeeds.Route}?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "search");
    }

    /// <summary>An enabled value that is not a switch is refused with 400 on the field rather than listing every rule.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_UnknownEnabledValue_IsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{TaxRuleTestSeeds.Route}?page=1&pageSize=20&enabled=maybe");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "enabled");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"{TaxRuleTestSeeds.Route}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Creates a rule through the admin API with a name this test controls.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="name">The rule's display name.</param>
    /// <param name="country">The two-letter country code.</param>
    /// <param name="region">The region, or <see langword="null"/> for a country-wide rule.</param>
    /// <param name="isEnabled">Whether new invoices use the rule.</param>
    /// <returns>The new rule's id.</returns>
    private static Task<int> CreateAsync(HttpClient admin, string name, string country, string? region, bool isEnabled) =>
        StorefrontContentTestSeeds.CreateAsync(
            admin, TaxRuleTestSeeds.Route, TaxRuleTestSeeds.Body(country, region, 19m, isEnabled, name));

    /// <summary>
    /// Seeds an enabled country-wide rule, a disabled regional rule of the same country and an enabled rule of a later one.
    /// A country holds one country-wide rule, so each test names its own pair of countries.
    /// </summary>
    /// <param name="country">The country of the country-wide and the regional rule.</param>
    /// <param name="otherCountry">The country of the later rule, sorted after <paramref name="country"/>.</param>
    /// <returns>The mix.</returns>
    private async Task<RuleMix> SeedMixAsync(string country, string otherCountry)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var region = await CreateAsync(admin, $"{token} region", country, $"Gauteng{token}", false);
        var other = await CreateAsync(admin, $"{token} other", otherCountry, null, true);
        var wide = await CreateAsync(admin, $"{token} wide", country, null, true);
        return new RuleMix(token, wide, region, other);
    }

    /// <summary>Lists the rules the admin list answers for a query string.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <returns>The ids of the page in the order the list gave them, and the total count of matches.</returns>
    private async Task<(List<int> Ids, int TotalCount)> ListAsync(string queryString)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"{TaxRuleTestSeeds.Route}?{queryString}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        return (ids, body.RootElement.GetProperty("totalCount").GetInt32());
    }
}
