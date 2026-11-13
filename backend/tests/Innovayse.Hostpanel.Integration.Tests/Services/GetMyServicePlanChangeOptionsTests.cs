using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/me/services/{id}/plan-change-options</c>: the plans a service can move to, each with its type
/// and the prorated amount the client sees before confirming, and none of the plans it cannot move to.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyServicePlanChangeOptionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the options of a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(int serviceId) => $"/api/me/services/{serviceId}/plan-change-options";

    /// <summary>
    /// Adds a product to the group of a seeded service.
    /// </summary>
    /// <param name="seeded">The seeded service, whose group the product joins.</param>
    /// <param name="name">The product's name.</param>
    /// <param name="usdMonthly">Its monthly USD price, or <see langword="null"/> for no USD price at all.</param>
    /// <param name="active">Whether the product is active.</param>
    /// <returns>The new product's id.</returns>
    private async Task<int> AddSiblingAsync(SeededPlanChange seeded, string name, decimal? usdMonthly, bool active = true)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var groupId = (await context.Products.AsNoTracking().FirstAsync(p => p.Id == seeded.FromProductId)).GroupId;
        var product = Product.Create(groupId, name, null, null, null, name.ToLowerInvariant(), ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
        if (usdMonthly is { } price)
        {
            product.SetPrice("USD", BillingCycle.Monthly, price);
        }

        if (!active)
        {
            product.Deactivate();
        }

        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        return product.Id;
    }

    /// <summary>The client sees the dearer and the cheaper plans of the group, each with its type and prorated amount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListsTheOtherPlansOfTheGroup_WithTheirTypeAndProratedAmount()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "options-list");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        var cheaper = await AddSiblingAsync(seeded, "Mini", 4m);

        var response = await owner.GetAsync(RouteOf(seeded.ServiceId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        options.Select(o => o.GetProperty("productId").GetInt32()).Should().BeEquivalentTo([seeded.ToProductId, cheaper]);

        var up = options.Single(o => o.GetProperty("productId").GetInt32() == seeded.ToProductId);
        up.GetProperty("type").GetString().Should().Be(nameof(ServiceChangeType.Upgrade));
        up.GetProperty("name").GetString().Should().Be("Pro");
        up.GetProperty("newRecurringAmount").GetDecimal().Should().Be(20m);
        up.GetProperty("prorationAmount").GetDecimal().Should().Be(ServiceChangeTestSeeds.ExpectedProration(10m, 20m, seeded.NextRenewalAt));
        up.GetProperty("currencyCode").GetString().Should().Be("USD");

        var down = options.Single(o => o.GetProperty("productId").GetInt32() == cheaper);
        down.GetProperty("type").GetString().Should().Be(nameof(ServiceChangeType.Downgrade));
        down.GetProperty("prorationAmount").GetDecimal().Should().Be(ServiceChangeTestSeeds.ExpectedProration(10m, 4m, seeded.NextRenewalAt));
    }

    /// <summary>
    /// A plan at the same price, an inactive plan, a plan with no price in the client's currency, the current plan itself and a
    /// product of another group are all left out.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeavesOutWhatTheServiceCannotMoveTo()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "options-excluded");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 20m);
        await AddSiblingAsync(seeded, "Twin", 10m);
        await AddSiblingAsync(seeded, "Retired", 30m, active: false);
        await AddSiblingAsync(seeded, "Unpriced", null);
        await ProductsTestSeeds.SeedProductWithUsdPricesAsync(factory, (BillingCycle.Monthly, 50m));

        var response = await owner.GetAsync(RouteOf(seeded.ServiceId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        options.Select(o => o.GetProperty("productId").GetInt32()).Should().BeEquivalentTo([seeded.ToProductId]);
    }

    /// <summary>A service with nowhere to move is told so with an empty list, not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AServiceWithNoOtherPlan_GetsAnEmptyList()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "options-empty");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, clientId, 10m, 10m);

        var response = await owner.GetAsync(RouteOf(seeded.ServiceId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    /// <summary>A service that is not active has no plan to change.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APendingService_Returns400WithServiceNotActive()
    {
        var (owner, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "options-pending");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var response = await owner.GetAsync(RouteOf(serviceId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
    }

    /// <summary>A stranger is told the service does not exist, as for an id that names nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AStrangersService_Returns404WithServiceNotFound()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "options-owner");
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, ownerClientId, 10m, 20m);
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.GetAsync(RouteOf(seeded.ServiceId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Without a session the route is refused before ownership is considered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutASession_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(RouteOf(ServicesTestSeeds.NoSuchServiceId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
