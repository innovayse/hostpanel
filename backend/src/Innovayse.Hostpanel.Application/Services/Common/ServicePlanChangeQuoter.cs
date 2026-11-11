using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductCyclePrices;
using Innovayse.Hostpanel.Application.Products.Queries.GetProducts;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// The one place that decides which plans a service can move to and what each move costs: the options the client sees
/// before confirming and the quote the request is made from are the same figures, because both come from here.
/// </summary>
/// <remarks>
/// A plan is another active product of the service's own product group that has a price for the service's billing
/// cycle in the client's billing currency; the prices come from the Products module's own lookup and the amount from
/// <see cref="ServicePlanChangeProration"/>. Nothing is read from or written to a table other than through those.
/// </remarks>
public sealed class ServicePlanChangeQuoter
{
    /// <summary>The Wolverine bus the Products module is asked through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The currency the service's client is billed in, which the plan prices and the amounts are in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The clock the moment of the change is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the quoter.</summary>
    /// <param name="bus">The Wolverine bus the Products module is asked through.</param>
    /// <param name="payerCurrency">The currency the service's client is billed in.</param>
    /// <param name="clock">The clock the moment of the change is read from.</param>
    public ServicePlanChangeQuoter(IMessageBus bus, IPayerCurrencyResolver payerCurrency, IClock clock)
    {
        _bus = bus;
        _payerCurrency = payerCurrency;
        _clock = clock;
    }

    /// <summary>Quotes moving the service to one plan.</summary>
    /// <param name="service">The service to move.</param>
    /// <param name="toProductId">The plan asked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The quote, or <c>SERVICE_NOT_ACTIVE</c> when the service is not active, <c>SERVICE_CHANGE_NO_RENEWAL_DATE</c> when
    /// it has no renewal date to prorate against, <c>SERVICE_CHANGE_SAME_PRODUCT</c> when it already runs on the plan,
    /// <c>PRODUCT_NOT_FOUND</c> when the plan does not exist, <c>SERVICE_CHANGE_PRODUCT_NOT_AVAILABLE</c> when it is
    /// inactive or of another group, <c>SERVICE_CHANGE_PRICE_UNAVAILABLE</c> when it has no price for the cycle in the
    /// client's currency, or <c>SERVICE_CHANGE_SAME_PRICE</c> when it costs the same as the current plan.
    /// </returns>
    public async Task<Result<ServicePlanChangeOptionDto>> QuoteAsync(ClientService service, int toProductId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(service);

        var renewal = RequireRenewalDate(service);
        if (renewal.IsFailure)
        {
            return Result<ServicePlanChangeOptionDto>.FailureCode(renewal.ErrorCode!);
        }

        if (toProductId == service.ProductId)
        {
            return Result<ServicePlanChangeOptionDto>.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_SAME_PRODUCT);
        }

        var found = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(
            new GetProductsByIdsQuery([service.ProductId, toProductId]), ct);
        var current = found.Value?.FirstOrDefault(p => p.Id == service.ProductId);
        var target = found.Value?.FirstOrDefault(p => p.Id == toProductId);
        if (current is null || target is null)
        {
            return Result<ServicePlanChangeOptionDto>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        if (target.Status != ProductStatus.Active || target.GroupId != current.GroupId)
        {
            return Result<ServicePlanChangeOptionDto>.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_PRODUCT_NOT_AVAILABLE);
        }

        var currency = (await _payerCurrency.ForClientAsync(service.ClientId, ct)).Code;
        var prices = await PricesAsync([target.Id], service.BillingCycle, currency, ct);
        if (!prices.TryGetValue(target.Id, out var price))
        {
            return Result<ServicePlanChangeOptionDto>.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_PRICE_UNAVAILABLE);
        }

        var option = Option(service, renewal.Value, target, price, currency);
        return option is null
            ? Result<ServicePlanChangeOptionDto>.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_SAME_PRICE)
            : Result<ServicePlanChangeOptionDto>.Success(option);
    }

    /// <summary>Lists the plans the service can move to, each with its quote.</summary>
    /// <param name="service">The service to move.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plans, or <c>SERVICE_NOT_ACTIVE</c> / <c>SERVICE_CHANGE_NO_RENEWAL_DATE</c> as for
    /// <see cref="QuoteAsync"/>, or <c>PRODUCT_NOT_FOUND</c> when the service's own product is gone. A plan with no price
    /// for the cycle in the client's currency, or with the same price as the current one, is left out.
    /// </returns>
    public async Task<Result<IReadOnlyList<ServicePlanChangeOptionDto>>> OptionsAsync(ClientService service, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(service);

        var renewal = RequireRenewalDate(service);
        if (renewal.IsFailure)
        {
            return Result<IReadOnlyList<ServicePlanChangeOptionDto>>.FailureCode(renewal.ErrorCode!);
        }

        var own = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(
            new GetProductsByIdsQuery([service.ProductId]), ct);
        var current = own.Value?.FirstOrDefault();
        if (current is null)
        {
            return Result<IReadOnlyList<ServicePlanChangeOptionDto>>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        var group = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(
            new GetProductsQuery(current.GroupId, ActiveOnly: true, IncludeUnsellable: true), ct);
        var candidates = (group.Value ?? []).Where(p => p.Id != service.ProductId).ToList();

        var currency = (await _payerCurrency.ForClientAsync(service.ClientId, ct)).Code;
        var prices = await PricesAsync(candidates.Select(p => p.Id).ToList(), service.BillingCycle, currency, ct);

        IReadOnlyList<ServicePlanChangeOptionDto> options = candidates
            .Where(p => prices.ContainsKey(p.Id))
            .Select(p => Option(service, renewal.Value, p, prices[p.Id], currency))
            .OfType<ServicePlanChangeOptionDto>()
            .ToList();
        return Result<IReadOnlyList<ServicePlanChangeOptionDto>>.Success(options);
    }

    /// <summary>Checks the service can be prorated at all and reads its renewal date.</summary>
    /// <param name="service">The service to move.</param>
    /// <returns>The renewal date that ends the current cycle, or the refusal.</returns>
    private static Result<DateTimeOffset> RequireRenewalDate(ClientService service)
    {
        if (service.Status is not ServiceStatus.Active)
        {
            return Result<DateTimeOffset>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        }

        return service.NextRenewalAt is { } renewal
            ? Result<DateTimeOffset>.Success(renewal)
            : Result<DateTimeOffset>.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_NO_RENEWAL_DATE);
    }

    /// <summary>Works out the move to one plan at its price.</summary>
    /// <param name="service">The service to move.</param>
    /// <param name="renewal">The end of the service's current cycle.</param>
    /// <param name="target">The plan.</param>
    /// <param name="price">The plan's price for the service's cycle, in <paramref name="currency"/>.</param>
    /// <param name="currency">ISO 4217 alpha code of the client's billing currency.</param>
    /// <returns>The option, or <see langword="null"/> when the plan costs the same as the current one.</returns>
    private ServicePlanChangeOptionDto? Option(ClientService service, DateTimeOffset renewal, ProductDto target, decimal price, string currency)
    {
        var change = ServicePlanChangeProration.Calculate(
            service.RecurringAmount, price, service.BillingCycle, renewal, _clock.UtcNow, currency);
        return change is null
            ? null
            : new ServicePlanChangeOptionDto(target.Id, target.Name, change.Type, price, change.Amount, currency);
    }

    /// <summary>Reads the plans' stored prices for one cycle and currency from the Products module.</summary>
    /// <param name="productIds">The plans to price.</param>
    /// <param name="cycle">The service's billing cycle.</param>
    /// <param name="currency">ISO 4217 alpha code of the client's billing currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The price of every plan that is sold for that cycle in that currency, by product id.</returns>
    private async Task<IReadOnlyDictionary<int, decimal>> PricesAsync(
        IReadOnlyList<int> productIds, BillingCycle cycle, string currency, CancellationToken ct)
    {
        var lookups = productIds.Select(id => new ProductCyclePriceLookup(id, currency, cycle)).ToList();
        var prices = await _bus.InvokeAsync<Result<IReadOnlyList<ProductCyclePriceDto>>>(new GetProductCyclePricesQuery(lookups), ct);
        return (prices.Value ?? [])
            .Where(p => p.Amount.HasValue)
            .ToDictionary(p => p.ProductId, p => p.Amount.GetValueOrDefault());
    }
}
