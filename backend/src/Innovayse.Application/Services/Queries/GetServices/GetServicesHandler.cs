namespace Innovayse.Application.Services.Queries.GetServices;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns a paginated list of all client services for admin use, enriched with client, domain, and pricing data.</summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="productRepo">Product repository, for names and prices.</param>
/// <param name="clientRepo">Client repository, for the owner's name.</param>
/// <param name="domainRepo">Domain repository, for the linked domain name.</param>
/// <param name="payerCurrency">Resolves the currency each owning client is billed in.</param>
public sealed class GetServicesHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IClientRepository clientRepo,
    IDomainRepository domainRepo,
    IPayerCurrencyResolver payerCurrency)
{
    /// <summary>The stored cycle string that bills annually; anything else is read as monthly.</summary>
    private const string AnnualCycle = "annual";

    /// <summary>Price shown when the product has no price in the client's currency for that cycle.</summary>
    private const decimal NoPrice = 0m;

    /// <summary>
    /// Handles <see cref="GetServicesQuery"/> by resolving product names, client names,
    /// linked domains, and the price in each owning client's currency for the billing cycle.
    /// </summary>
    /// <param name="qry">The query containing pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result of enriched service list items.</returns>
    public async Task<PagedResult<ServiceListItemDto>> HandleAsync(GetServicesQuery qry, CancellationToken ct)
    {
        var (services, total) = await serviceRepo.ListAsync(qry.Page, qry.PageSize, qry.ClientId, ct);

        // Batch-resolve products
        var productIds = services.Select(s => s.ProductId).Distinct();
        var products = await productRepo.FindByIdsAsync(productIds, ct);
        var productMap = products.ToDictionary(p => p.Id);

        // Batch-resolve client names
        var clientIds = services.Select(s => s.ClientId).Distinct().ToList();
        var clients = await clientRepo.FindByIdsAsync(clientIds, ct);
        var clientMap = clients.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        // Resolve each owning client's billing currency once; a service is priced in that.
        var currencyMap = new Dictionary<int, Currency>();
        foreach (var clientId in clientIds)
        {
            currencyMap[clientId] = await payerCurrency.ForClientAsync(clientId, ct);
        }

        // Batch-resolve linked domain names
        var serviceIds = services.Select(s => s.Id);
        var domainMap = await domainRepo.FindDomainNamesByServiceIdsAsync(serviceIds, ct);

        var result = services
            .Select(svc =>
            {
                var product = productMap.GetValueOrDefault(svc.ProductId);
                var currency = currencyMap[svc.ClientId];
                var cycle = svc.BillingCycle == AnnualCycle ? BillingCycle.Annual : BillingCycle.Monthly;
                var price = product?.PriceFor(currency.Code, cycle) ?? NoPrice;

                return new ServiceListItemDto(
                    svc.Id,
                    svc.ClientId,
                    clientMap.GetValueOrDefault(svc.ClientId, "Unknown"),
                    product?.Name ?? "Unknown",
                    domainMap.GetValueOrDefault(svc.Id),
                    price,
                    currency.Code,
                    svc.BillingCycle,
                    svc.Status,
                    svc.NextRenewalAt);
            })
            .ToList();

        return new PagedResult<ServiceListItemDto>(result, total, qry.Page, qry.PageSize);
    }
}
