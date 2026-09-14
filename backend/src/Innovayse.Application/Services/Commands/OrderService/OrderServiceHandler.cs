namespace Innovayse.Application.Services.Commands.OrderService;

using Innovayse.Application.Billing.Interfaces;
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
/// <param name="payerCurrency">The currency the ordering client is billed in, for the fallback price.</param>
public sealed class OrderServiceHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IUnitOfWork uow,
    IStringLocalizer<ValidationMessages> localizer,
    IPayerCurrencyResolver payerCurrency)
{
    /// <summary>
    /// Handles <see cref="OrderServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created client service ID.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the product is not found or inactive, or when an amount must be read from the
    /// product and it has no price for the cycle in the client's currency.
    /// </exception>
    public async Task<int> HandleAsync(OrderServiceCommand cmd, CancellationToken ct)
    {
        var product = await productRepo.FindByIdAsync(cmd.ProductId, ct)
            ?? throw new InvalidOperationException(localizer["ProductNotFound", cmd.ProductId]);

        if (product.Status != ProductStatus.Active)
        {
            throw new InvalidOperationException(localizer["ProductNotAvailable", cmd.ProductId]);
        }

        // The order-fulfilment callers pass the amounts snapshotted on the order line — zero
        // included, which is a free period and stays free. The client's own route passes null and
        // the amounts come from the product's price in that client's currency. Only a null amount
        // consults the stored price, so a product with no price in that currency is refused
        // rather than sold for nothing, and an accepted order never fails fulfilment over it.
        var needsPrice = cmd.FirstPaymentAmount is null || cmd.RecurringAmount is null;
        var cyclePrice = needsPrice ? await ResolveCyclePriceAsync(product, cmd, ct) : 0m;
        var firstPayment = cmd.FirstPaymentAmount ?? cyclePrice;
        var recurring = cmd.RecurringAmount ?? cyclePrice;

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

    /// <summary>Reads the product's stored price for the ordered cycle in the client's currency.</summary>
    /// <param name="product">The product being ordered.</param>
    /// <param name="cmd">The order command, naming the client and the cycle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="InvalidOperationException">The product has no price for that cycle in that currency.</exception>
    private async Task<decimal> ResolveCyclePriceAsync(Product product, OrderServiceCommand cmd, CancellationToken ct)
    {
        var currency = await payerCurrency.ForClientAsync(cmd.ClientId, ct);
        var cycle = BillingCycleParser.Parse(cmd.BillingCycle);
        return product.PriceFor(currency.Code, cycle)
            ?? throw new InvalidOperationException(localizer["ProductNotAvailable", cmd.ProductId]);
    }
}
