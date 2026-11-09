using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetLinkedDomainNames;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductCyclePrices;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServices;

/// <summary>Returns a paginated list of all client services for admin use, enriched with client, domain, and pricing data.</summary>
public sealed class GetServicesHandler
{
    /// <summary>Price shown when the product has no price in the client's currency for that cycle.</summary>
    private const decimal NoPrice = 0m;

    /// <summary>The module's data context the services are read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the product, client and linked domain queries are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Resolves the currency each owning client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context the services are read from.</param>
    /// <param name="bus">The bus the product, client and linked domain queries are dispatched through.</param>
    /// <param name="payerCurrency">Resolves the currency each owning client is billed in.</param>
    public GetServicesHandler(IHostpanelDbContext context, IMessageBus bus, IPayerCurrencyResolver payerCurrency)
    {
        _context = context;
        _bus = bus;
        _payerCurrency = payerCurrency;
    }

    /// <summary>
    /// Handles the query by resolving product names, client names, linked domains, and the price
    /// in each owning client's currency for the billing cycle.
    /// </summary>
    /// <param name="qry">The page, page size and optional client filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, always: an out-of-range page answers an empty page, not a refusal.</returns>
    /// <exception cref="InvalidOperationException">A service names a product or a client that does not exist, so the database is broken.</exception>
    public async Task<Result<PaginatedList<ServiceListItemDto>>> HandleAsync(GetServicesQuery qry, CancellationToken ct)
    {
        var servicesQuery = _context.ClientServices.AsNoTracking();
        if (qry.ClientId.HasValue)
        {
            servicesQuery = servicesQuery.Where(s => s.ClientId == qry.ClientId.Value);
        }

        var total = await servicesQuery.CountAsync(ct);
        var services = await servicesQuery
            .OrderByDescending(s => s.CreatedAt)
            .Paginate(qry.Page, qry.PageSize)
            .ToListAsync(ct);

        // Batch-resolve product names; FK_ClientServices_Products_ProductId keeps every product alive.
        var productIds = services.Select(s => s.ProductId).Distinct().ToList();
        var products = await _bus.GetRequiredProductSummariesByIdAsync(productIds, ct);

        // Batch-resolve client names; FK_ClientServices_Clients_ClientId keeps every owner alive.
        var clientIds = services.Select(s => s.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        // Resolve each owning client's billing currency once; a service is priced in that.
        var currencyMap = new Dictionary<int, Currency>();
        foreach (var clientId in clientIds)
        {
            currencyMap[clientId] = await _payerCurrency.ForClientAsync(clientId, ct);
        }

        // Batch-resolve linked domain names; a service that runs on a site of its own, not on a registered domain, shows its own domain
        var serviceIds = services.Select(s => s.Id).ToList();
        var domains = await _bus.InvokeAsync<Result<IReadOnlyList<LinkedDomainNameDto>>>(
            new GetLinkedDomainNamesQuery(serviceIds), ct);
        var domainMap = (domains.Value ?? []).ToDictionary(d => d.ServiceId, d => d.Name);

        // Batch-resolve each service's price in its owner's currency through the Products module,
        // which prices through the product itself; the answer keeps the lookups' order.
        var lookups = services
            .Select(svc => new ProductCyclePriceLookup(
                svc.ProductId,
                currencyMap[svc.ClientId].Code,
                svc.BillingCycle))
            .Distinct()
            .ToList();
        var prices = await _bus.InvokeAsync<Result<IReadOnlyList<ProductCyclePriceDto>>>(
            new GetProductCyclePricesQuery(lookups), ct);
        var priceMap = (prices.Value ?? []).ToDictionary(
            p => new ProductCyclePriceLookup(p.ProductId, p.CurrencyCode, p.Cycle),
            p => p.Amount);

        var result = services
            .Select(svc =>
            {
                var currency = currencyMap[svc.ClientId];
                var price = priceMap.GetValueOrDefault(new ProductCyclePriceLookup(svc.ProductId, currency.Code, svc.BillingCycle)) ?? NoPrice;

                return new ServiceListItemDto(
                    svc.Id,
                    svc.ClientId,
                    clients[svc.ClientId].FullName,
                    products[svc.ProductId].Name,
                    domainMap.GetValueOrDefault(svc.Id) ?? svc.Domain,
                    price,
                    currency.Code,
                    svc.BillingCycle,
                    svc.Status,
                    svc.NextRenewalAt);
            })
            .ToList();

        return Result<PaginatedList<ServiceListItemDto>>.Success(
            new PaginatedList<ServiceListItemDto>(result, total, qry.Page, qry.PageSize));
    }
}
