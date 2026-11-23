using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductCyclePrices;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Application.Services.Commands.ActivateDigitalService;
using Innovayse.Hostpanel.Application.Services.Commands.OrderService;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.StartMyTrial;

/// <summary>Resolves the caller's client record, then starts the free trial of a product for it.</summary>
public sealed class StartMyTrialHandler
{
    /// <summary>Wolverine bus the trial is dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Resolves the account the caller acts for, owner or linked user.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the currency the account pays in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Wolverine bus the trial is dispatched through.</param>
    /// <param name="access">Resolves the account the caller acts for, owner or linked user.</param>
    /// <param name="payerCurrency">Resolves the currency the account pays in.</param>
    public StartMyTrialHandler(IMessageBus bus, IClientAccess access, IPayerCurrencyResolver payerCurrency)
    {
        _bus = bus;
        _access = access;
        _payerCurrency = payerCurrency;
    }

    /// <summary>Starts the trial of a product for the account the caller acts for.</summary>
    /// <param name="cmd">The product.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new service id, <c>CLIENT_PROFILE_NOT_FOUND</c>, <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the account
    /// may not order, <c>PRODUCT_NOT_FOUND</c>, <c>PRODUCT_NOT_AVAILABLE</c> when the product is not active or has no priced
    /// billing cycle in the client's currency, <c>PRODUCT_TRIAL_NOT_AVAILABLE</c> when it offers no trial, or the order's
    /// own refusal such as <c>ORDER_TRIAL_ALREADY_USED</c>.
    /// </returns>
    public async Task<Result<int>> HandleAsync(StartMyTrialCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<int>.FailureCode(clientId.ErrorCode!);
        }

        var mayOrder = await _bus.EnsureClientMayTransactAsync(clientId.Value, ct);
        if (mayOrder.IsFailure)
        {
            return Result<int>.FailureCode(mayOrder.ErrorCode!);
        }

        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(new GetProductsByIdsQuery([cmd.ProductId]), ct);
        var product = products.Value?.FirstOrDefault();
        if (product is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        if (product.Status != ProductStatus.Active)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE);
        }

        if (!product.Trial.Enabled)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_TRIAL_NOT_AVAILABLE);
        }

        // The cheapest priced cycle carries the trial, so the conversion later knows what the client will pay. A price of
        // zero or less counts as unpriced; a tie goes to the earlier cycle in BillingCycle enum order (OrderBy is stable).
        var currency = await _payerCurrency.ForClientAsync(clientId.Value, ct);
        var lookups = Enum.GetValues<BillingCycle>().Select(c => new ProductCyclePriceLookup(product.Id, currency.Code, c)).ToList();
        var prices = await _bus.InvokeAsync<Result<IReadOnlyList<ProductCyclePriceDto>>>(new GetProductCyclePricesQuery(lookups), ct);
        var cheapest = (prices.Value ?? [])
            .Where(p => p.Amount is > 0)
            .OrderBy(p => p.Amount)
            .FirstOrDefault();
        if (cheapest is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE);
        }

        // Never with an order line: a trial is started by the client, not fulfilled from an order.
        var ordered = await _bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId.Value, cmd.ProductId, cheapest.Cycle, 0m, null, Trial: product.Trial.ToDomain()),
            ct);
        if (ordered.IsFailure || product.Type != ProductType.Digital)
        {
            // A hosting trial stays Pending until the client runs the normal setup (POST api/me/services/{id}/setup).
            return ordered;
        }

        // A product with no hosting module has nothing to set up, so nothing else would ever activate its trial.
        var activated = await _bus.InvokeAsync<Result>(new ActivateDigitalServiceCommand(ordered.Value), ct);
        return activated.IsFailure ? Result<int>.FailureCode(activated.ErrorCode!) : ordered;
    }
}
