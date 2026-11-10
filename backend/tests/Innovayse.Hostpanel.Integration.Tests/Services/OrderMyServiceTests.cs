using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for the <c>OrderMyService</c> use case: <c>POST /api/me/services</c> resolves
/// the caller inside the handler and orders for that account, priced from the product.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrderMyServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The order lands on the caller's own account and is priced from the product, whatever amounts the body carries.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderCreatesTheServiceForTheCallerAtTheProductPrice()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            (_, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 4.5m);
        }

        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "order-my-service");

        var response = await client.PostAsJsonAsync("/api/me/services", new
        {
            productId,
            billingCycle = nameof(BillingCycle.Monthly),
            firstPaymentAmount = 0m,
            recurringAmount = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var serviceId = await response.Content.ReadFromJsonAsync<int>();
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId);
        service.ClientId.Should().Be(clientId);
        service.FirstPaymentAmount.Should().Be(4.5m);
        service.RecurringAmount.Should().Be(4.5m);
    }

    /// <summary>An unknown product answers 404 with <c>PRODUCT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderRefusesAnUnknownProduct()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "order-my-service-missing");

        var response = await client.PostAsJsonAsync("/api/me/services", new
        {
            productId = 999999,
            billingCycle = nameof(BillingCycle.Monthly),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/services", new
        {
            productId = 1,
            billingCycle = nameof(BillingCycle.Monthly),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
