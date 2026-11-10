using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/services</c>, the admin paged list of client services.
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

        var response = await client.GetAsync("/api/services");

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
            var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
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
            var service = ClientService.Create(ids.ClientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
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
}
