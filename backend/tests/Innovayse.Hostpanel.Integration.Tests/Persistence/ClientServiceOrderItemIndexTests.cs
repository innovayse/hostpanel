using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The filtered unique index on the order line of a client service, against Postgres: a line has one service, a second row
/// for it is refused as the lost race it is (never as a raw database error), and services that belong to no line may repeat.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientServiceOrderItemIndexTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A second service for one order line is refused with the persistence-agnostic conflict, and only the first is kept.</summary>
    [Fact]
    public async Task SecondServiceForTheSameOrderLine_IsRefusedAsAConflict()
    {
        var (clientId, productId) = await SeedClientAndProductAsync();
        var line = 616_101;
        await AddAsync(clientId, productId, line);

        var act = () => AddAsync(clientId, productId, line);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().CountAsync(s => s.OrderItemId == line, CancellationToken.None)).Should().Be(1);
    }

    /// <summary>Services that were not ordered through a line carry no line id and may repeat freely.</summary>
    [Fact]
    public async Task ServicesWithoutAnOrderLine_MayRepeat()
    {
        var (clientId, productId) = await SeedClientAndProductAsync();

        await AddAsync(clientId, productId, orderItemId: null);
        await AddAsync(clientId, productId, orderItemId: null);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId, CancellationToken.None)).Should().Be(2);
    }

    /// <summary>Seeds a client and an active product to order.</summary>
    /// <returns>The client's and the product's primary keys.</returns>
    private async Task<(int ClientId, int ProductId)> SeedClientAndProductAsync()
    {
        using var scope = factory.Services.CreateScope();
        var ids = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        return (ids.ClientId, ids.ProductId);
    }

    /// <summary>Saves a new ordered service for the line in its own scope.</summary>
    /// <param name="clientId">The ordering client.</param>
    /// <param name="productId">The ordered product.</param>
    /// <param name="orderItemId">The order line, or <see langword="null"/> for a service not ordered through a line.</param>
    /// <returns>A task that completes once the service is saved.</returns>
    private async Task AddAsync(int clientId, int productId, int? orderItemId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.ClientServices.Add(ClientService.CreateOrdered(
            clientId, productId, BillingCycle.Monthly, domain: null, 5m, 5m, "Card", firstInvoiceId: null, DateTimeOffset.UtcNow, orderItemId));
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
