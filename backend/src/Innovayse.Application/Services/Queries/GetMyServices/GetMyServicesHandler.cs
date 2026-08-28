namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns the calling client's services as DTOs with product names.</summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="productRepo">Product repository, for the display names.</param>
/// <param name="serverRepo">Server repository, for the host each service sits on.</param>
/// <param name="clientRepo">Resolves the caller's client record.</param>
/// <param name="caller">Who is asking; the query does not say, and must not.</param>
public sealed class GetMyServicesHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IServerRepository serverRepo,
    IClientRepository clientRepo,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Handles <see cref="GetMyServicesQuery"/>.
    /// </summary>
    /// <param name="qry">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of client service DTOs.</returns>
    /// <exception cref="ClientProfileNotFoundException">Thrown when the caller has no client record.</exception>
    public async Task<IReadOnlyList<ClientServiceDto>> HandleAsync(GetMyServicesQuery qry, CancellationToken ct)
    {
        var userId = caller.RequireUserId();
        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        var services = await serviceRepo.ListByClientAsync(client.Id, ct);

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
