using System.Globalization;
using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/services</c>, the admin paged list of client services, narrowed by a search on the
/// domain, the product name and the service id and by owner, status, product and billing cycle, all before paging.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET /api/services without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client calling GET /api/services is refused: the policy is admin only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_Returns403_AsPlainClientAsync()
    {
        var (client, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-services-client");

        var response = await client.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Admin GET /api/services returns 200 OK with paged results.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_Returns200_AsAdminAsync()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/services?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The list answers the shared page shape: items with page number, size, total count and total pages.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_AnswersTheSharedPageShapeAsync()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/services?page=2&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        root.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("pageNumber").GetInt32().Should().Be(2);
        root.GetProperty("pageSize").GetInt32().Should().Be(5);
        root.TryGetProperty("totalCount", out _).Should().BeTrue();
        root.TryGetProperty("totalPages", out _).Should().BeTrue();
        root.TryGetProperty("page", out _).Should().BeFalse();
    }

    /// <summary>A service that runs on a site of its own, not on a registered domain, shows that site in the admin list instead of an empty cell, beside its owner's name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_ShowsTheServicesOwnDomainWhenNoRegisteredDomainIsLinkedAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var domain = $"site-{Guid.NewGuid():N}.example";
        int serviceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 5m);
            var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, TestMoments.Seeded);
            service.ChangeBillingTerms(new ServiceBillingTerms(
                BillingCycle: BillingCycle.Monthly, Quantity: 1, FirstPaymentAmount: 5m,
                RecurringAmount: 5m, NextRenewalAt: null, PaymentMethod: null,
                PromotionCode: null, SubscriptionId: null));
            service.RecordHostingAccount(new ServiceHostingAccount(
                ServerId: null, Domain: domain, ProvisioningRef: null, DedicatedIp: null, Username: null, Password: null));
            context.ClientServices.Add(service);
            await context.SaveChangesAsync(CancellationToken.None);
            serviceId = service.Id;
        }

        var response = await admin.GetAsync("/api/services?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("domain").GetString().Should().Be(domain);
        row.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
        row.GetProperty("productName").GetString().Should().Be("Starter");
    }

    /// <summary>The list's price is the one the product entity resolves for the owner's currency and the service's cycle.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_PriceEqualsTheProductsOwnResolutionAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        int serviceId;
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var ids = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 7.25m);
            productId = ids.ProductId;
            var service = ClientService.Create(ids.ClientId, productId, BillingCycle.Monthly, TestMoments.Seeded);
            context.ClientServices.Add(service);
            await context.SaveChangesAsync(CancellationToken.None);
            serviceId = service.Id;
        }

        var response = await admin.GetAsync("/api/services?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == serviceId);
        var currency = row.GetProperty("priceCurrency").GetString()!;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var product = context.Products.Include(p => p.Prices).Single(p => p.Id == productId);
            var expected = product.PriceFor(currency, BillingCycle.Monthly) ?? 0m;
            row.GetProperty("price").GetDecimal().Should().Be(expected);
        }
    }

    /// <summary>The search matches the service's own domain, in any letter case, and leaves the other services out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_SearchMatchesTheOwnDomainCaseInsensitiveAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (ids, total) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search=PENDING-{mix.Token.ToUpperInvariant()}");

        ids.Should().Equal(mix.Pending);
        total.Should().Be(1);
    }

    /// <summary>The search matches the registered domain linked to a service that has no domain of its own.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_SearchMatchesTheLinkedRegisteredDomainAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (ids, total) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search=linked-{mix.Token}");

        ids.Should().Equal(mix.Suspended);
        total.Should().Be(1);
    }

    /// <summary>A service with several linked registered domains is found by any of them, not only the first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_SearchMatchesAnyOfSeveralLinkedDomainsAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var second = RegisteredDomain.Import(
                mix.ClientId, $"second-{mix.Token}.com", DomainStatus.Active, TestMoments.Seeded, TestMoments.Seeded.AddYears(1),
                autoRenew: false, TestCurrencies.Base, registrar: null, registrarModule: null);
            second.LinkService(mix.Suspended);
            context.Domains.Add(second);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var (first, firstTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search=linked-{mix.Token}");
        var (other, otherTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search=second-{mix.Token}");

        first.Should().Equal(mix.Suspended);
        firstTotal.Should().Be(1);
        other.Should().Equal(mix.Suspended);
        otherTotal.Should().Be(1);
    }

    /// <summary>The search matches the name of the ordered product, so the services of one product are found by it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_SearchMatchesTheProductNameAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (alpha, alphaTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search=alpha-{mix.Token}");
        var (beta, betaTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search=beta-{mix.Token}");

        alpha.Should().Equal(mix.Active, mix.Pending);
        alphaTotal.Should().Be(2);
        beta.Should().Equal(mix.Terminated, mix.Suspended);
        betaTotal.Should().Be(2);
    }

    /// <summary>A search that is a whole number finds the service with that id, and only it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_SearchMatchesTheServiceIdAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        // The digits of an id can also sit inside a random token, so the test searches for an id that none of the
        // mix's own domains and product names contain, which makes the one match certain.
        var texts = new[] { $"pending-{mix.Token}", $"active-{mix.Token}", $"gone-{mix.Token}", $"linked-{mix.Token}", $"alpha-{mix.Token}", $"beta-{mix.Token}" };
        var searched = new[] { mix.Pending, mix.Active, mix.Suspended, mix.Terminated }
            .First(id => !texts.Any(text => text.Contains(id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)));

        var (ids, total) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&clientId={mix.ClientId}&search={searched}");

        ids.Should().Equal(searched);
        total.Should().Be(1);
    }

    /// <summary>A percent or an underscore typed into the search matches itself, not any text or any character.</summary>
    /// <param name="wildcard">The character the admin typed.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("%25")]
    [InlineData("_")]
    public async Task GetServicesAsync_SearchWildcardIsLiteralAsync(string wildcard)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        // "pending-<token>" holds a hyphen where the wildcard would stand, so only a wildcard reading finds it.
        var (inside, insideTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search=pending{wildcard}{mix.Token}");
        var (alone, aloneTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={wildcard}");

        inside.Should().BeEmpty();
        insideTotal.Should().Be(0);
        alone.Should().BeEmpty();
        aloneTotal.Should().Be(0);
    }

    /// <summary>The status filter keeps only the services in that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_StatusFilterKeepsOnlyThatStatusAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (suspended, suspendedTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={mix.Token}&status=Suspended");
        var (pending, _) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={mix.Token}&status=Pending");
        var (terminated, _) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={mix.Token}&status=Terminated");

        suspended.Should().Equal(mix.Suspended);
        suspendedTotal.Should().Be(1);
        pending.Should().Equal(mix.Pending);
        terminated.Should().Equal(mix.Terminated);
    }

    /// <summary>The product filter keeps only the services of that product, named by its id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_ProductFilterKeepsOnlyThatProductAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (alpha, alphaTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&productId={mix.AlphaProductId}");
        var (beta, _) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&productId={mix.BetaProductId}");

        alpha.Should().Equal(mix.Active, mix.Pending);
        alphaTotal.Should().Be(2);
        beta.Should().Equal(mix.Terminated, mix.Suspended);
    }

    /// <summary>The billing cycle filter keeps only the services billed on that cycle.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_BillingCycleFilterKeepsOnlyThatCycleAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (monthly, monthlyTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={mix.Token}&billingCycle=Monthly");
        var (annual, _) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={mix.Token}&billingCycle=Annual");
        var (quarterly, _) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={mix.Token}&billingCycle=Quarterly");
        var (biennial, biennialTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=100&search={mix.Token}&billingCycle=Biennial");

        monthly.Should().Equal(mix.Suspended, mix.Pending);
        monthlyTotal.Should().Be(2);
        annual.Should().Equal(mix.Active);
        quarterly.Should().Equal(mix.Terminated);
        biennial.Should().BeEmpty();
        biennialTotal.Should().Be(0);
    }

    /// <summary>The owner, search, status, product and billing cycle filters narrow together: a service must pass every one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_CombinedFiltersAllApplyAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (statusAndProduct, _) = await ServicesTestSeeds.ListServiceIdsAsync(
            admin, $"page=1&pageSize=100&status=Suspended&productId={mix.BetaProductId}");
        var (allFilters, _) = await ServicesTestSeeds.ListServiceIdsAsync(
            admin, $"page=1&pageSize=100&clientId={mix.ClientId}&search=beta-{mix.Token}&status=Terminated&productId={mix.BetaProductId}&billingCycle=Quarterly");
        var (wrongCycle, wrongCycleTotal) = await ServicesTestSeeds.ListServiceIdsAsync(
            admin, $"page=1&pageSize=100&search={mix.Token}&status=Terminated&billingCycle=Monthly");
        var (otherOwner, otherOwnerTotal) = await ServicesTestSeeds.ListServiceIdsAsync(
            admin, $"page=1&pageSize=100&search={mix.Token}&clientId={mix.ClientId + 1_000_000}");

        statusAndProduct.Should().Equal(mix.Suspended);
        allFilters.Should().Equal(mix.Terminated);
        wrongCycle.Should().BeEmpty();
        wrongCycleTotal.Should().Be(0);
        otherOwner.Should().BeEmpty();
        otherOwnerTotal.Should().Be(0);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_FilterIsAppliedBeforePagingAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var mix = await ServicesTestSeeds.SeedServiceMixAsync(factory);

        var (first, firstTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=1&pageSize=3&search={mix.Token}");
        var (second, secondTotal) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=2&pageSize=3&search={mix.Token}");
        var (beyond, _) = await ServicesTestSeeds.ListServiceIdsAsync(admin, $"page=3&pageSize=3&search={mix.Token}");

        first.Should().Equal(mix.Terminated, mix.Suspended, mix.Active);
        second.Should().Equal(mix.Pending);
        firstTotal.Should().Be(4);
        secondTotal.Should().Be(4);
        beyond.Should().BeEmpty();
    }

    /// <summary>A missing or non-positive page is refused with the page sentence rather than defaulted.</summary>
    /// <param name="queryString">The query string sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("pageSize=20")]
    [InlineData("page=0&pageSize=20")]
    public async Task GetServicesAsync_MissingOrZeroPage_Returns400WithThePageMessageAsync(string queryString)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
        (await ServicesTestSeeds.FieldMessagesAsync(response, "Page")).Should().Equal("Page must be 1 or greater.");
    }

    /// <summary>A page size over the limit, or none, is refused with the limits in the sentence rather than clamped.</summary>
    /// <param name="queryString">The query string sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=1&pageSize=101")]
    [InlineData("page=1")]
    public async Task GetServicesAsync_BadPageSize_Returns400WithTheLimitsAsync(string queryString)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, "PageSize")).Should().Equal("Page size must be between 1 and 100.");
    }

    /// <summary>A search longer than the limit is refused with the limit in the sentence.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetServicesAsync_SearchTooLong_Returns400WithTheLimitAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services?page=1&pageSize=20&search={new string('a', 201)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, "Search")).Should().Equal("The search text may be at most 200 characters.");
    }

    /// <summary>
    /// A status that names nothing, by name or by number, is refused with 400 under the status field. The model binder
    /// refuses it before the validator runs, so the sentence is the binder's and names the value sent; the validator's own
    /// sentence is pinned in <see cref="GetServicesValidatorTests"/>.
    /// </summary>
    /// <param name="status">The status value sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Nonsense")]
    [InlineData("99")]
    public async Task GetServicesAsync_UnknownStatus_Returns400NamingTheStatusFieldAsync(string status)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services?page=1&pageSize=20&status={status}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, "Status")).Should().ContainSingle().Which.Should().Contain(status);
    }

    /// <summary>
    /// A billing cycle that names nothing, by name or by number, is refused with 400 under the billing cycle field, by the
    /// model binder as for the status.
    /// </summary>
    /// <param name="billingCycle">The billing cycle value sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Weekly")]
    [InlineData("99")]
    public async Task GetServicesAsync_UnknownBillingCycle_Returns400NamingTheCycleFieldAsync(string billingCycle)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services?page=1&pageSize=20&billingCycle={billingCycle}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, "BillingCycle")).Should().ContainSingle().Which.Should().Contain(billingCycle);
    }

    /// <summary>A product or client id that is not positive is refused with its own sentence.</summary>
    /// <param name="key">The query key sent.</param>
    /// <param name="field">The field the refusal names.</param>
    /// <param name="message">The sentence expected.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("productId", "ProductId", "Provide a valid product id.")]
    [InlineData("clientId", "ClientId", "Provide a valid client to filter by.")]
    public async Task GetServicesAsync_NonPositiveId_Returns400WithTheIdMessageAsync(string key, string field, string message)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services?page=1&pageSize=20&{key}=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, field)).Should().Equal(message);
    }
}
