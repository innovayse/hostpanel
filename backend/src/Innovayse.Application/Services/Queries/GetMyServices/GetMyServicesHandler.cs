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
        var productMap = products.ToDictionary(p => p.Id, p => p.Name);

        var serverIds = services.Where(s => s.ServerId.HasValue).Select(s => s.ServerId!.Value).Distinct();
        var servers = new Dictionary<int, (string Name, string Hostname, string? IpAddress)>();
        foreach (var sid in serverIds)
        {
            var server = await serverRepo.FindByIdAsync(sid, ct);
            if (server is not null)
            {
                servers[sid] = (server.Name, server.Hostname, server.IpAddress);
            }
        }

        return services
            .Select(svc =>
            {
                string? serverName = null;
                string? serverHostname = null;
                string? serverIp = null;
                if (svc.ServerId.HasValue && servers.TryGetValue(svc.ServerId.Value, out var srv))
                {
                    serverName = srv.Name;
                    serverHostname = srv.Hostname;
                    serverIp = srv.IpAddress;
                }

                return new ClientServiceDto(
                    svc.Id,
                    svc.ProductId,
                    productMap.GetValueOrDefault(svc.ProductId, "Unknown"),
                    svc.BillingCycle,
                    svc.Status,
                    svc.NextRenewalAt,
                    svc.Domain,
                    svc.Username,
                    svc.RecurringAmount,
                    svc.FirstPaymentAmount,
                    svc.PaymentMethod,
                    svc.ServerId,
                    serverName,
                    serverHostname,
                    serverIp);
            })
            .ToList();
    }
}
