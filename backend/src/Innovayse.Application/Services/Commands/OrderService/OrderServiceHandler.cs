namespace Innovayse.Application.Services.Commands.OrderService;

using Innovayse.Application.Common;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Application.Resources;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Localization;

/// <summary>
/// Creates a pending <see cref="ClientService"/> record for the ordered product.
/// Provisioning is handled asynchronously by an event handler that listens
/// for <c>ClientServiceCreatedEvent</c>.
/// </summary>
/// <param name="serviceRepo">Client service repository the new record is added to.</param>
/// <param name="productRepo">Product repository, for the product being ordered.</param>
/// <param name="uow">Unit of work for persisting changes.</param>
/// <param name="localizer">The refusal sentences, in the caller's own language.</param>
public sealed class OrderServiceHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IUnitOfWork uow,
    IStringLocalizer<ValidationMessages> localizer)
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
            ?? throw new InvalidOperationException(localizer["ProductNotFound", cmd.ProductId]);

        if (product.Status != ProductStatus.Active)
        {
            throw new InvalidOperationException(localizer["ProductNotAvailable", cmd.ProductId]);
        }

        var cyclePrice = cmd.BillingCycle == "annual" ? product.AnnualPrice : product.MonthlyPrice;
        var firstPayment = cmd.FirstPaymentAmount > 0 ? cmd.FirstPaymentAmount : cyclePrice;
        var recurring = cmd.RecurringAmount > 0 ? cmd.RecurringAmount : cyclePrice;

        var service = ClientService.Create(cmd.ClientId, cmd.ProductId, cmd.BillingCycle);

        service.Update(
            domain: cmd.Domain, dedicatedIp: null, username: null,
            password: null, billingCycle: cmd.BillingCycle,
            recurringAmount: recurring, paymentMethod: cmd.PaymentMethod,
            nextRenewalAt: null, subscriptionId: null,
            overrideAutoSuspend: false, suspendUntil: null,
            autoTerminateEndOfCycle: false, autoTerminateReason: null,
            adminNotes: null, provisioningRef: null,
            firstPaymentAmount: firstPayment,
            promotionCode: null, terminatedAt: null,
            serverId: null, quantity: 1, productId: null);

        serviceRepo.Add(service);
        await uow.SaveChangesAsync(ct);
        return service.Id;
    }
}
