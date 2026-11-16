using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Queries.CountProductCommitments;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListCatalogProducts;

/// <summary>
/// Returns the storefront catalogue: active products priced in the caller's currency, without the
/// fields only staff should see.
/// </summary>
public sealed class ListCatalogProductsHandler
{
    /// <summary>The module's data context, and the configured currencies, to validate an anonymous caller's requested code.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The currency the caller is billed in; a guest gets the base.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The current request's caller, to tell a guest from a signed-in client.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>The bus the Orders module's count of taken places is asked on.</summary>
    private readonly IMessageBus _bus;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context, and the configured currencies, to validate an anonymous caller's requested code.</param>
    /// <param name="payerCurrency">The currency the caller is billed in; a guest gets the base.</param>
    /// <param name="caller">The current request's caller, to tell a guest from a signed-in client.</param>
    /// <param name="bus">The bus the Orders module's count of taken places is asked on.</param>
    public ListCatalogProductsHandler(
        IHostpanelDbContext context,
        IPayerCurrencyResolver payerCurrency,
        ICurrentUserService caller,
        IMessageBus bus)
    {
        _context = context;
        _payerCurrency = payerCurrency;
        _caller = caller;
        _bus = bus;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="qry">The group filter and requested currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The active products the caller can buy in the resolved currency, in the operator's order, leaving out a
    /// product whose cap on live services is reached (sold out). Never refuses.
    /// </returns>
    /// <remarks>
    /// A signed-in client is always billed in their own recorded currency, so
    /// <see cref="ListCatalogProductsQuery.Currency"/> is only consulted for an anonymous caller,
    /// as in <c>GetTldPricingHandler</c>; an unknown or disabled code is ignored rather than rejected.
    /// </remarks>
    public async Task<Result<IReadOnlyList<CatalogProductDto>>> HandleAsync(ListCatalogProductsQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var currency = await ResolveCurrencyAsync(qry.Currency, ct);

        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Prices)
            .Where(p => p.Status == ProductStatus.Active);
        if (qry.GroupId.HasValue)
        {
            query = query.Where(p => p.GroupId == qry.GroupId.Value);
        }

        if (qry.ProductId.HasValue)
        {
            query = query.Where(p => p.Id == qry.ProductId.Value);
        }

        if (qry.GroupIds is { Length: > 0 })
        {
            var groupIds = qry.GroupIds;
            query = query.Where(p => groupIds.Contains(p.GroupId));
        }

        var products = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);

        products = products.Where(p => p.SellsIn(currency)).ToList();

        // A product whose cap on live services is reached is sold out: the storefront stops offering it, and
        // the order flow would refuse it anyway.
        var capped = products.Where(p => p.MaxActiveServices is not null).Select(p => p.Id).ToList();
        if (capped.Count > 0)
        {
            var taken = await _bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(
                new CountProductCommitmentsQuery(capped), ct);
            if (taken.IsSuccess)
            {
                products = products
                    .Where(p => p.MaxActiveServices is not { } max || taken.Value!.GetValueOrDefault(p.Id) < max)
                    .ToList();
            }
        }

        IReadOnlyList<CatalogProductDto> dtos = products
            .Select(p => p.ToCatalogDto(currency))
            .ToList();

        return Result<IReadOnlyList<CatalogProductDto>>.Success(dtos);
    }

    /// <summary>
    /// Picks the currency to price this response in: an anonymous caller's explicit, enabled
    /// request wins; otherwise falls back to the caller's resolved billing currency (the client's
    /// own currency for a signed-in caller, the base for a guest with no valid request).
    /// </summary>
    /// <param name="requested">The <see cref="ListCatalogProductsQuery.Currency"/> value, if any.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ISO 4217 alpha code to price in.</returns>
    private async Task<string> ResolveCurrencyAsync(string? requested, CancellationToken ct)
    {
        if (_caller.UserId is null && !string.IsNullOrWhiteSpace(requested))
        {
            var offered = await _context.FindOfferedCurrencyAsync(requested, ct);
            if (offered is not null)
            {
                return offered.Code;
            }
        }

        return (await _payerCurrency.ForCallerAsync(ct)).Code;
    }
}
