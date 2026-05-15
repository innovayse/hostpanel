namespace Innovayse.Application.Services.Commands.OrderService;

using Innovayse.Application.Common;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Creates a pending <see cref="ClientService"/> record for the ordered product.
/// Provisioning is handled asynchronously by an event handler that listens
/// for <c>ClientServiceCreatedEvent</c>.
/// </summary>
public sealed class OrderServiceHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="OrderServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created client service ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the product is not found or inactive.</exception>
    public async Task<int> HandleAsync(OrderServiceCommand cmd, CancellationToken ct)
    {
        var product = await productRepo.FindByIdAsync(cmd.ProductId, ct)
            ?? throw new InvalidOperationException($"Product {cmd.ProductId} not found.");

        if (product.Status != ProductStatus.Active)
        {
            throw new InvalidOperationException($"Product {cmd.ProductId} is not available for ordering.");
        }

        var service = ClientService.Create(cmd.ClientId, cmd.ProductId, cmd.BillingCycle);
        serviceRepo.Add(service);
        await uow.SaveChangesAsync(ct);
        return service.Id;
    }
}
