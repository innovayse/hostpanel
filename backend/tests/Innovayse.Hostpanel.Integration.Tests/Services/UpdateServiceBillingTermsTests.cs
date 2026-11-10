using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceBillingTerms;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>PUT /api/services/{id}/billing</c> and <see cref="UpdateServiceBillingTermsHandler"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateServiceBillingTermsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A valid save answers 204 and stores every billing term, leaving the rest of the service alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateBillingTerms_OnAnExistingService_StoresTheTermsAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var productId = await ProductOfAsync(serviceId);
        var renewal = new DateTimeOffset(2031, 3, 4, 0, 0, 0, TimeSpan.Zero);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/billing", new
        {
            productId,
            quantity = 2,
            billingCycle = nameof(BillingCycle.Annual),
            firstPaymentAmount = 12.5m,
            recurringAmount = 120m,
            nextRenewalAt = renewal,
            paymentMethod = "card",
            promotionCode = "SPRING",
            subscriptionId = "sub-1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.Quantity.Should().Be(2);
        stored.BillingCycle.Should().Be(BillingCycle.Annual);
        stored.FirstPaymentAmount.Should().Be(12.5m);
        stored.RecurringAmount.Should().Be(120m);
        stored.NextRenewalAt.Should().Be(renewal);
        stored.PaymentMethod.Should().Be("card");
        stored.PromotionCode.Should().Be("SPRING");
        stored.SubscriptionId.Should().Be("sub-1");
        stored.Status.Should().Be(ServiceStatus.Active);
        stored.ProvisioningRef.Should().Be("acct1");
    }

    /// <summary>Moving the service to a product that does not exist answers 404 <c>PRODUCT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateBillingTerms_ToAnUnknownProduct_Returns404ProductNotFoundAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/billing", ValidBody(productId: 999_999_999));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A negative amount is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateBillingTerms_WithANegativeAmount_Returns400ValidationErrorAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/billing", new
        {
            productId = await ProductOfAsync(serviceId),
            quantity = 1,
            billingCycle = nameof(BillingCycle.Monthly),
            firstPaymentAmount = 1m,
            recurringAmount = -1m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>A billing cycle the parser does not know is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateBillingTerms_WithAnUnknownBillingCycle_Returns400ValidationErrorAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/billing", new
        {
            productId = await ProductOfAsync(serviceId),
            quantity = 1,
            billingCycle = "fortnightly",
            firstPaymentAmount = 1m,
            recurringAmount = 1m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Saving the terms of a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateBillingTerms_OnAnUnknownService_Returns404Async()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/billing", ValidBody(productId: 1));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Saving without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateBillingTerms_WithoutAuth_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/billing", ValidBody(productId: 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A body the validator accepts, naming the given product.</summary>
    /// <param name="productId">The product the service is to be of.</param>
    /// <returns>The anonymous body.</returns>
    private static object ValidBody(int productId) => new
    {
        productId,
        quantity = 1,
        billingCycle = nameof(BillingCycle.Monthly),
        firstPaymentAmount = 1m,
        recurringAmount = 1m,
    };

    /// <summary>Reads the product a stored service is of.</summary>
    /// <param name="serviceId">The service primary key.</param>
    /// <returns>The product id.</returns>
    private async Task<int> ProductOfAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => s.ProductId)
            .SingleAsync();
    }
}
