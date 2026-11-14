using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>ListDomains</c> use case: <c>GET /api/domains</c>, the admin
/// paginated listing of every registered domain.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListDomainsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An unauthenticated caller is refused with 401 before any query runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/domains");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A signed-in Client, who does not hold the Admin role this
    /// admin-only listing requires, is refused with 403.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminOnly_ClientForbiddenAsync()
    {
        var (client, _, _) = await DomainsTestSeeds.RegisterClientAsync(factory, "list-domains-forbidden");

        var response = await client.GetAsync("/api/domains");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>An admin lists domains and sees the one just registered for a client, named after that client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_AdminSeesARegisteredDomainAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, ownerClientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "list-domains-owner");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, ownerClientId);

        var response = await admin.GetAsync("/api/domains?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Single(d => d.GetProperty("id").GetInt32() == domainId)
            .GetProperty("clientName").GetString().Should().Be("Domain Tester");
    }

    /// <summary>A page number below one is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_PageBelowOne_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/domains?page=0&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>The search matches the domain name case-insensitively as a substring and leaves other domains out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_SearchFiltersByNameCaseInsensitiveAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, ownerClientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "list-domains-search");
        var token = Guid.NewGuid().ToString("N")[..10];
        var matchId = await DomainsTestSeeds.RegisterDomainAsync(admin, ownerClientId, $"shop-{token}.com");
        var otherId = await DomainsTestSeeds.RegisterDomainAsync(admin, ownerClientId);

        var response = await admin.GetAsync($"/api/domains?page=1&pageSize=100&search={token.ToUpperInvariant()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain(matchId).And.NotContain(otherId);
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    /// <summary>A search wildcard typed by the admin matches itself, not every domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_SearchWildcardIsLiteralAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/domains?page=1&pageSize=100&search=%25");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    /// <summary>A search longer than the limit is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_SearchTooLong_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/domains?page=1&pageSize=20&search={new string('a', 256)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>The domains one seeded mix holds, by the role each plays in the filter tests.</summary>
    /// <param name="Token">The text every seeded name carries, so a search scopes a request to this mix.</param>
    /// <param name="ClientId">The owning client's id.</param>
    /// <param name="ActiveNameAm">An active domain at the NameAm module.</param>
    /// <param name="ExpiredNamecheap">An expired domain at the Namecheap module.</param>
    /// <param name="ActiveNamecheap">An active domain at the Namecheap module.</param>
    /// <param name="ActiveLegacy">An active domain with no registrar module recorded.</param>
    private sealed record DomainMix(
        string Token, int ClientId, int ActiveNameAm, int ExpiredNamecheap, int ActiveNamecheap, int ActiveLegacy);

    /// <summary>Seeds one owner with four domains that differ in status and registrar module.</summary>
    /// <returns>The mix, with the names ordered a, b, c, d so the list order is known.</returns>
    private async Task<DomainMix> SeedMixAsync()
    {
        var (_, ownerClientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "list-domains-mix");
        var token = Guid.NewGuid().ToString("N")[..10];
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var activeNameAm = await DomainsTestSeeds.SeedDomainAsync(
            context, ownerClientId, $"a-{token}.com", DomainStatus.Active, RegistrarModule.NameAm);
        var expiredNamecheap = await DomainsTestSeeds.SeedDomainAsync(
            context, ownerClientId, $"b-{token}.com", DomainStatus.Expired, RegistrarModule.Namecheap);
        var activeNamecheap = await DomainsTestSeeds.SeedDomainAsync(
            context, ownerClientId, $"c-{token}.com", DomainStatus.Active, RegistrarModule.Namecheap);
        var activeLegacy = await DomainsTestSeeds.SeedDomainAsync(
            context, ownerClientId, $"d-{token}.com", DomainStatus.Active, registrarModule: null);
        return new DomainMix(token, ownerClientId, activeNameAm, expiredNamecheap, activeNamecheap, activeLegacy);
    }

    /// <summary>Lists the domains the admin list answers for a query string.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <returns>The ids of the page in the order the list gave them, and the total count of matches.</returns>
    private async Task<(List<int> Ids, int TotalCount)> ListAsync(string queryString)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"/api/domains?{queryString}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetInt32()).ToList();
        return (ids, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    /// <summary>The status filter keeps only the domains in that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_StatusFilterKeepsOnlyThatStatusAsync()
    {
        var mix = await SeedMixAsync();

        var (expired, expiredCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&status=Expired");
        var (active, activeCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&status=Active");

        expired.Should().Equal(mix.ExpiredNamecheap);
        expiredCount.Should().Be(1);
        active.Should().Equal(mix.ActiveNameAm, mix.ActiveNamecheap, mix.ActiveLegacy);
        activeCount.Should().Be(3);
    }

    /// <summary>The registrar filter keeps only the domains of that module and leaves out the ones with none recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_RegistrarFilterKeepsOnlyThatModuleAsync()
    {
        var mix = await SeedMixAsync();

        var (nameAm, _) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&registrarModule=NameAm");
        var (namecheap, namecheapCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&registrarModule=Namecheap");

        nameAm.Should().Equal(mix.ActiveNameAm);
        namecheap.Should().Equal(mix.ExpiredNamecheap, mix.ActiveNamecheap);
        namecheapCount.Should().Be(2);
    }

    /// <summary>The status, registrar, owner and search filters narrow together: a domain must pass every one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_CombinedFiltersAllApplyAsync()
    {
        var mix = await SeedMixAsync();

        var (statusAndRegistrar, _) = await ListAsync(
            $"page=1&pageSize=100&search={mix.Token}&status=Active&registrarModule=Namecheap");
        var (withOwner, _) = await ListAsync(
            $"page=1&pageSize=100&search=c-{mix.Token}&status=Active&registrarModule=Namecheap&clientId={mix.ClientId}");
        var (otherOwner, otherOwnerCount) = await ListAsync(
            $"page=1&pageSize=100&search={mix.Token}&status=Active&clientId={mix.ClientId + 1_000_000}");

        statusAndRegistrar.Should().Equal(mix.ActiveNamecheap);
        withOwner.Should().Equal(mix.ActiveNamecheap);
        otherOwner.Should().BeEmpty();
        otherOwnerCount.Should().Be(0);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, in name order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_FilterIsAppliedBeforePagingAsync()
    {
        var mix = await SeedMixAsync();

        var (first, firstCount) = await ListAsync($"page=1&pageSize=2&search={mix.Token}&status=Active");
        var (second, secondCount) = await ListAsync($"page=2&pageSize=2&search={mix.Token}&status=Active");
        var (beyond, _) = await ListAsync($"page=3&pageSize=2&search={mix.Token}&status=Active");

        first.Should().Equal(mix.ActiveNameAm, mix.ActiveNamecheap);
        second.Should().Equal(mix.ActiveLegacy);
        firstCount.Should().Be(3);
        secondCount.Should().Be(3);
        beyond.Should().BeEmpty();
    }

    /// <summary>A page size over the limit is refused with 400 rather than clamped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_PageSizeOverTheLimit_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/domains?page=1&pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>A status or registrar that names nothing, by name or by number, is refused with 400.</summary>
    /// <param name="filter">The filter's query string, key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("status=Nonsense")]
    [InlineData("status=99")]
    [InlineData("registrarModule=Nonsense")]
    [InlineData("registrarModule=99")]
    public async Task ListDomains_UnknownFilterValue_Returns400Async(string filter)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/domains?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
