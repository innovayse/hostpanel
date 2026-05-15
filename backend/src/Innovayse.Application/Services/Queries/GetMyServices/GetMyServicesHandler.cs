namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns all services for a client as DTOs with product names.</summary>
public sealed class GetMyServicesHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo)
{
    /// <summary>
    /// Handles <see cref="GetMyServicesQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of client service DTOs.</returns>
    public async Task<IReadOnlyList<ClientServiceDto>> HandleAsync(GetMyServicesQuery qry, CancellationToken ct)
    {
        var services = await serviceRepo.ListByClientAsync(qry.ClientId, ct);

        var productIds = services.Select(s => s.ProductId).Distinct();
        var products = await productRepo.FindByIdsAsync(productIds, ct);
        var productMap = products.ToDictionary(p => p.Id, p => p.Name);

        return services
            .Select(svc => new ClientServiceDto(
                svc.Id,
                svc.ProductId,
                productMap.GetValueOrDefault(svc.ProductId, "Unknown"),
                svc.BillingCycle,
                svc.Status,
                svc.NextRenewalAt))
            .ToList();
    }
}
