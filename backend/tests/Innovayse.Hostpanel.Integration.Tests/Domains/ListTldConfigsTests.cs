using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>ListTldConfigs</c> use case:
/// <c>GET /api/admin/tld-configs</c>, the paged, searchable and filterable list. The happy path (create then list)
/// also lives in <see cref="CreateTldConfigTests"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTldConfigsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the list.</summary>
    private const string Route = "/api/admin/tld-configs";

    /// <summary>Listing never refuses a valid page: it answers 200 even when a search matches nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsOkWithAnEmptyPageWhenNothingMatchesAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..10];

        var (ids, totalCount) = await ListAsync($"page=1&pageSize=20&search=none{token}");

        ids.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    /// <summary>
    /// The margin compares the sell price with the cost converted into the sell currency through the stored rates, so a
    /// cost stated in a small currency is not read against a sell price in a large one.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_MarginComparesTheSellPriceWithTheCostConvertedIntoTheSellCurrencyAsync()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQA", "964", 0.5m);
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQB", "965", 1m);
        var tld = $"m{Guid.NewGuid():N}"[..10];
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await DomainsTestSeeds.SeedPricedTldConfigAsync(context, tld, "ZQA", "ZQB", 5m, 10m);
        }

        var margin = await MarginOfAsync(tld);

        // 5 ZQA is 2.5 ZQB; selling at 10 ZQB earns 300 percent, not the 100 percent the raw numbers suggest.
        margin.GetDecimal().Should().Be(300m);
    }

    /// <summary>A cost in the sell currency needs no conversion: the margin is the plain difference.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_MarginOfASingleCurrencyNeedsNoConversionAsync()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQB", "965", 1m);
        var tld = $"s{Guid.NewGuid():N}"[..10];
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await DomainsTestSeeds.SeedPricedTldConfigAsync(context, tld, "ZQB", "ZQB", 5m, 10m);
        }

        var margin = await MarginOfAsync(tld);

        margin.GetDecimal().Should().Be(100m);
    }

    /// <summary>A cost whose currency has no stored rate has no margin: it is null, never a guess.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_MarginIsNullWhenTheCostCurrencyHasNoRateAsync()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQB", "965", 1m);
        var tld = $"n{Guid.NewGuid():N}"[..10];
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await DomainsTestSeeds.SeedPricedTldConfigAsync(context, tld, "ZQN", "ZQB", 5m, 10m);
        }

        var margin = await MarginOfAsync(tld);

        margin.ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"{Route}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The search matches the TLD in any letter case, as a substring, and leaves the other TLDs out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchMatchesTheTldCaseInsensitivelyAsync()
    {
        var mix = await SeedMixAsync();

        var (ids, totalCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token.ToUpperInvariant()}b");

        ids.Should().Equal(mix.Second);
        totalCount.Should().Be(1);
    }

    /// <summary>A leading dot typed in the search is ignored, because a TLD is stored without its dot.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchIgnoresALeadingDotAsync()
    {
        var mix = await SeedMixAsync();

        var (ids, totalCount) = await ListAsync($"page=1&pageSize=100&search=.{mix.Token}a");

        ids.Should().Equal(mix.First);
        totalCount.Should().Be(1);
    }

    /// <summary>A <c>%</c> or <c>_</c> typed in the search matches itself, not any text or any single character.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchWildcardsMatchThemselvesAsync()
    {
        var mix = await SeedMixAsync();

        var (percent, percentCount) = await ListAsync("page=1&pageSize=100&search=%25");
        var (underscore, _) = await ListAsync($"page=1&pageSize=100&search={mix.Token}_");
        var (anyText, anyTextCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}%25a");

        percent.Should().BeEmpty();
        percentCount.Should().Be(0);
        underscore.Should().Equal(mix.Underscored);
        anyText.Should().BeEmpty();
        anyTextCount.Should().Be(0);
    }

    /// <summary>The registrar filter keeps only the TLDs sourced from that module.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RegistrarFilterKeepsOnlyThatModuleAsync()
    {
        var mix = await SeedMixAsync();

        var (nameAm, _) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&registrarModule=NameAm");
        var (namecheap, namecheapCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&registrarModule=Namecheap");

        nameAm.Should().Equal(mix.First);
        namecheap.Should().Equal(mix.Underscored, mix.Second);
        namecheapCount.Should().Be(2);
    }

    /// <summary>The enabled filter keeps the TLDs on sale or the ones switched off, and no filter keeps both.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_EnabledFilterKeepsOnlyThatStateAsync()
    {
        var mix = await SeedMixAsync();

        var (enabled, enabledCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&enabled=true");
        var (disabled, disabledCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&enabled=false");
        var (both, bothCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}");

        enabled.Should().Equal(mix.Underscored, mix.First);
        enabledCount.Should().Be(2);
        disabled.Should().Equal(mix.Second);
        disabledCount.Should().Be(1);
        both.Should().HaveCount(3);
        bothCount.Should().Be(3);
    }

    /// <summary>The registrar and enabled filters narrow together: a TLD must pass both.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_CombinedFiltersAllApplyAsync()
    {
        var mix = await SeedMixAsync();

        var (ids, totalCount) = await ListAsync(
            $"page=1&pageSize=100&search={mix.Token}&registrarModule=Namecheap&enabled=true");

        ids.Should().Equal(mix.Underscored);
        totalCount.Should().Be(1);
    }

    /// <summary>
    /// The list is in sort order, then TLD, and a filter is applied before paging: the total counts every match and each page
    /// holds its slice.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IsOrderedAndPagedAfterTheFiltersAsync()
    {
        var mix = await SeedMixAsync();

        var (all, _) = await ListAsync($"page=1&pageSize=100&search={mix.Token}");
        var (first, firstCount) = await ListAsync($"page=1&pageSize=2&search={mix.Token}");
        var (second, secondCount) = await ListAsync($"page=2&pageSize=2&search={mix.Token}");
        var (beyond, _) = await ListAsync($"page=3&pageSize=2&search={mix.Token}");

        all.Should().Equal(mix.Underscored, mix.First, mix.Second);
        first.Should().Equal(mix.Underscored, mix.First);
        second.Should().Equal(mix.Second);
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
    public async Task List_PagingOutsideTheLimits_IsRefusedAsync(string queryString, string field)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?{queryString}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchOverTheLimit_IsRefusedAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "search");
    }

    /// <summary>A registrar or enabled value that names nothing, by name or by number, is refused with 400 rather than listing everything.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("registrarModule=Nonsense", "registrarModule")]
    [InlineData("registrarModule=99", "registrarModule")]
    [InlineData("enabled=maybe", "enabled")]
    public async Task List_UnknownFilterValue_IsRefusedAsync(string filter, string field)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?page=1&pageSize=20&{filter}");

        // The model binder refuses a value that is no member before the validator runs, so the check is that the field is named.
        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>The TLD configurations one seeded mix holds, by the role each plays in the tests.</summary>
    /// <param name="Token">The text every seeded TLD carries, so a search scopes a request to this mix.</param>
    /// <param name="Underscored">An enabled Namecheap TLD with an underscore after the token, sort order 1.</param>
    /// <param name="First">An enabled NameAm TLD, sort order 2.</param>
    /// <param name="Second">A disabled Namecheap TLD, sort order 2, sorted after <paramref name="First"/> by its TLD.</param>
    private sealed record TldMix(string Token, int Underscored, int First, int Second);

    /// <summary>Seeds three TLD configurations that differ in registrar module, enabled state and sort order.</summary>
    /// <returns>The mix.</returns>
    private async Task<TldMix> SeedMixAsync()
    {
        var token = $"q{Guid.NewGuid():N}"[..10];
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var underscored = await DomainsTestSeeds.SeedTldConfigAsync(context, $"{token}_u", RegistrarModule.Namecheap, true, 1);
        var first = await DomainsTestSeeds.SeedTldConfigAsync(context, $"{token}a", RegistrarModule.NameAm, true, 2);
        var second = await DomainsTestSeeds.SeedTldConfigAsync(context, $"{token}b", RegistrarModule.Namecheap, false, 2);
        return new TldMix(token, underscored, first, second);
    }

    /// <summary>Reads the margin the list answers for one TLD.</summary>
    /// <param name="tld">The extension, without its dot.</param>
    /// <returns>The row's <c>marginPercent</c> member.</returns>
    private async Task<JsonElement> MarginOfAsync(string tld)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"{Route}?page=1&pageSize=20&search={tld}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return page.RootElement.GetProperty("items").EnumerateArray().Single().GetProperty("marginPercent").Clone();
    }

    /// <summary>Lists the configurations the admin list answers for a query string.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <returns>The ids of the page in the order the list gave them, and the total count of matches.</returns>
    private async Task<(List<int> Ids, int TotalCount)> ListAsync(string queryString)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"{Route}?{queryString}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetInt32()).ToList();
        return (ids, body.RootElement.GetProperty("totalCount").GetInt32());
    }
}
