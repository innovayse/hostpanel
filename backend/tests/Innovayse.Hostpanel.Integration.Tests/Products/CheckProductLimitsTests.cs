using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Products.Queries.CheckProductLimits;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary><see cref="CheckProductLimitsQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckProductLimitsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A product with neither a cap nor a one-per-client rule always has room, however many places are asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnlimitedProduct_AlwaysHasRoomAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: null, onePerClient: false);

        var outcome = await CheckAsync(new CheckProductLimitsQuery([productId, productId, productId]));

        outcome.IsSuccess.Should().BeTrue();
    }

    /// <summary>The places asked for count against the cap together with the services already live.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_CappedProduct_CountsTheLiveServicesAndThePlacesAskedAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: 3, onePerClient: false);
        var (_, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "check-cap");
        await AddServicesAsync(clientId, productId, count: 2);

        (await CheckAsync(new CheckProductLimitsQuery([productId]))).IsSuccess.Should().BeTrue();

        var over = await CheckAsync(new CheckProductLimitsQuery([productId, productId]));
        over.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
    }

    /// <summary>A one-per-client product is refused to a client who holds one, and when the request names it twice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_OnePerClientProduct_RefusesAHolderAndADuplicateAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: null, onePerClient: true);
        var (_, holderId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "check-holder");
        var (_, otherId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "check-other");
        await AddServicesAsync(holderId, productId, count: 1);

        var held = await CheckAsync(new CheckProductLimitsQuery([productId], holderId));
        var twice = await CheckAsync(new CheckProductLimitsQuery([productId, productId], otherId));
        var free = await CheckAsync(new CheckProductLimitsQuery([productId], otherId));

        held.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);
        twice.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);
        free.IsSuccess.Should().BeTrue();
    }

    /// <summary>Runs the check through the bus.</summary>
    /// <param name="query">The check to run.</param>
    /// <returns>The handler's outcome.</returns>
    private async Task<Result> CheckAsync(CheckProductLimitsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(query);
    }

    /// <summary>Gives the client live services of the product.</summary>
    /// <param name="clientId">The client who holds them.</param>
    /// <param name="productId">The product they are of.</param>
    /// <param name="count">How many to add.</param>
    /// <returns>A task that completes once they are saved.</returns>
    private async Task AddServicesAsync(int clientId, int productId, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.ClientServices.Add(ClientService.Create(clientId, productId, BillingCycle.Monthly, TestMoments.Seeded));
        }

        await db.SaveChangesAsync();
    }
}
