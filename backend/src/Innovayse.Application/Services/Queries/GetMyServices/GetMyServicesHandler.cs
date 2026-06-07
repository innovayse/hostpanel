namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns all services for a client as DTOs with product names.</summary>
public sealed class GetMyServicesHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IServerRepository serverRepo)
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
        var productMap = products.ToDictionary(p => p.Id);

        var serverIds = services.Where(s => s.ServerId.HasValue).Select(s => s.ServerId!.Value).Distinct();
        var servers = await serverRepo.FindByIdsAsync(serverIds, ct);
        var serverMap = servers.ToDictionary(s => s.Id);

        return services
            .Select(svc =>
            {
                var product = productMap.GetValueOrDefault(svc.ProductId);
                var server = svc.ServerId.HasValue ? serverMap.GetValueOrDefault(svc.ServerId.Value) : null;
                return new ClientServiceDto(
                    svc.Id,
                    svc.ProductId,
                    product?.Name ?? "Unknown",
                    product?.Type ?? Domain.Products.ProductType.Other,
                    svc.BillingCycle,
                    svc.Status,
                    svc.NextRenewalAt,
                    svc.Domain,
                    svc.Username,
                    svc.RecurringAmount,
                    server?.IpAddress,
                    server?.Hostname,
                    server?.Ns1Hostname,
                    server?.Ns2Hostname,
                    svc.TouchEstatePublicKey,
                    svc.TouchEstateSecretKey);
            })
            .ToList();
    }
}
