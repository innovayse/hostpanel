namespace Innovayse.Application.Services.Queries.GetServices;

using Innovayse.Application.Common;
using Innovayse.Application.Services.DTOs;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns a paginated list of all client services for admin use, enriched with client, domain, and pricing data.</summary>
public sealed class GetServicesHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IClientRepository clientRepo,
    IDomainRepository domainRepo)
{
    /// <summary>
    /// Handles <see cref="GetServicesQuery"/> by resolving product names, client names,
    /// linked domains, and pricing from the billing cycle.
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
        var clientIds = services.Select(s => s.ClientId).Distinct();
        var clients = await clientRepo.FindByIdsAsync(clientIds, ct);
        var clientMap = clients.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        // Batch-resolve linked domain names
        var serviceIds = services.Select(s => s.Id);
        var domainMap = await domainRepo.FindDomainNamesByServiceIdsAsync(serviceIds, ct);

        var result = services
            .Select(svc =>
            {
                var product = productMap.GetValueOrDefault(svc.ProductId);
                var price = svc.BillingCycle == "annual"
                    ? product?.AnnualPrice ?? 0
                    : product?.MonthlyPrice ?? 0;

                return new ServiceListItemDto(
                    svc.Id,
                    svc.ClientId,
                    clientMap.GetValueOrDefault(svc.ClientId, "Unknown"),
                    product?.Name ?? "Unknown",
                    domainMap.GetValueOrDefault(svc.Id),
                    price,
                    "USD",
                    svc.BillingCycle,
                    svc.Status,
                    svc.NextRenewalAt);
            })
            .ToList();

        return new PagedResult<ServiceListItemDto>(result, total, qry.Page, qry.PageSize);
    }
}
