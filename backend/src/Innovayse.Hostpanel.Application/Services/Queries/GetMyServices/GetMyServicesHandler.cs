using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerBriefs;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Queries.GetDiskUsageByDomains;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetMyServices;

/// <summary>Returns the calling client's services as DTOs with product names.</summary>
public sealed class GetMyServicesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reaches the Products and Servers modules for the names behind the ids.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Resolves the account the caller acts for, owner or linked user; the query does not say whose, and must not.</summary>
    private readonly IClientAccess _access;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reaches the Products and Servers modules for the names behind the ids.</param>
    /// <param name="access">Resolves the account the caller acts for, owner or linked user; the query does not say whose, and must not.</param>
    public GetMyServicesHandler(IHostpanelDbContext context, IMessageBus bus, IClientAccess access)
    {
        _context = context;
        _bus = bus;
        _access = access;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="qry">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The services of the account the caller acts for, or <c>CLIENT_PROFILE_NOT_FOUND</c> when the
    /// caller acts for no account.
    /// </returns>
    /// <exception cref="InvalidOperationException">A service names a product that does not exist, so the database is broken.</exception>
    public async Task<Result<IReadOnlyList<ClientServiceDto>>> HandleAsync(
        GetMyServicesQuery qry, CancellationToken ct)
    {
        var client = await _access.RequireClientIdAsync(ct);
        if (client.IsFailure)
        {
            return Result<IReadOnlyList<ClientServiceDto>>.FailureCode(client.ErrorCode!);
        }

        var clientId = client.Value;

        // Read-only, so no tracking is needed.
        var services = await _context.ClientServices
            .AsNoTracking()
            .Where(s => s.ClientId == clientId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        // Products and servers belong to other modules: every distinct id is asked through their query once.
        // FK_ClientServices_Products_ProductId restricts deleting a product that has services, so every product is there.
        var productIds = services.Select(s => s.ProductId).Distinct().ToList();
        var products = await _bus.GetRequiredProductSummariesByIdAsync(productIds, ct);

        var serverIds = services.Where(s => s.ServerId.HasValue).Select(s => s.ServerId!.Value).Distinct().ToList();
        var serverBriefs = await _bus.InvokeAsync<Result<IReadOnlyList<ServerBriefDto>>>(
            new GetServerBriefsQuery(serverIds), ct);
        var servers = (serverBriefs.Value ?? []).ToDictionary(s => s.Id);

        // The server reports usage per account, keyed by its own name and the account's domain;
        // the Reports module owns the samples.
        var domains = services
            .Where(s => s.Domain != null)
            .Select(s => s.Domain!.ToLowerInvariant())
            .Distinct()
            .ToList();
        var samples = await _bus.InvokeAsync<Result<IReadOnlyList<DomainDiskUsageDto>>>(
            new GetDiskUsageByDomainsQuery(domains), ct);
        var usage = (samples.Value ?? []).ToDictionary(u => (u.ServerName, u.Domain));

        var dtos = services
            .Select(svc =>
            {
                ServiceServerDto? server = null;
                if (svc.ServerId.HasValue && servers.TryGetValue(svc.ServerId.Value, out var srv))
                {
                    server = new ServiceServerDto(srv.Name, srv.Hostname, srv.IpAddress);
                }

                ServiceUsageDto? serviceUsage = null;
                if (server != null && svc.Domain != null
                    && usage.TryGetValue((server.Name, svc.Domain.ToLowerInvariant()), out var stat))
                {
                    serviceUsage = new ServiceUsageDto(stat.DiskUsedMb, stat.DiskLimitMb, stat.BandwidthUsedMb, stat.BandwidthLimitMb, stat.UpdatedAt);
                }

                return new ClientServiceDto(
                    svc.Id,
                    svc.ProductId,
                    products[svc.ProductId].Name,
                    products[svc.ProductId].Fulfilment.IsDigital,
                    svc.BillingCycle,
                    svc.Status,
                    svc.NextRenewalAt,
                    svc.Domain,
                    svc.Username,
                    svc.RecurringAmount,
                    svc.FirstPaymentAmount,
                    svc.PaymentMethod,
                    server,
                    svc.CreatedAt,
                    svc.Status == ServiceStatus.Suspended ? svc.SuspensionSource : null,
                    serviceUsage);
            })
            .ToList();

        return Result<IReadOnlyList<ClientServiceDto>>.Success(dtos);
    }
}
