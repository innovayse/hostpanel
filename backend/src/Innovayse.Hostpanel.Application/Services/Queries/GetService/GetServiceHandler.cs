using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerBriefs;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetService;

/// <summary>Returns a single enriched client service for admin detail views.</summary>
public sealed class GetServiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the product, client and server queries are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the product, client and server queries are dispatched through.</param>
    public GetServiceHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>
    /// Handles the query by loading the service and resolving the product name, client name, and
    /// server name.
    /// </summary>
    /// <param name="qry">The service to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The enriched detail, or <c>SERVICE_NOT_FOUND</c> when the id names no service.</returns>
    /// <exception cref="InvalidOperationException">The service names a product or a client that does not exist, so the database is broken.</exception>
    public async Task<Result<ServiceDetailDto>> HandleAsync(GetServiceQuery qry, CancellationToken ct)
    {
        // Read-only, so no tracking is needed.
        var svc = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == qry.ServiceId, ct);
        if (svc is null)
        {
            return Result<ServiceDetailDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        // The product, client and server rows belong to other modules, so each is asked through its query.
        // FK_ClientServices_Products_ProductId and FK_ClientServices_Clients_ClientId restrict deleting a
        // product or a client that has services, so a missing one is a broken database, not an answer to render.
        var product = await _bus.GetRequiredProductSummaryAsync(svc.ProductId, ct);
        var client = await _bus.GetRequiredClientBriefAsync(svc.ClientId, withEmail: false, ct);

        ServerBriefDto? server = null;
        if (svc.ServerId is { } serverId)
        {
            var servers = await _bus.InvokeAsync<Result<IReadOnlyList<ServerBriefDto>>>(
                new GetServerBriefsQuery([serverId]), ct);
            server = servers.Value?.FirstOrDefault();
        }

        var assignedServer = server is null ? null : new ServiceServerDto(server.Name, server.Hostname, server.IpAddress);

        var dto = new ServiceDetailDto(
            svc.Id,
            svc.ClientId,
            client.FullName,
            svc.ProductId,
            product.Name,
            svc.Domain,
            svc.Status,
            svc.CreatedAt,
            svc.TerminatedAt,
            svc.AdminNotes,
            svc.ServerId,
            assignedServer,
            new ServiceAccountDto(svc.DedicatedIp, svc.Username, svc.Password, svc.ProvisioningRef),
            new ServiceBillingTermsDto(
                svc.Quantity,
                svc.FirstPaymentAmount,
                svc.RecurringAmount,
                svc.PaymentMethod,
                svc.PromotionCode,
                svc.SubscriptionId,
                svc.BillingCycle,
                svc.NextRenewalAt,
                svc.AutoRenew),
            new ServiceAutomationDto(
                svc.OverrideAutoSuspend,
                svc.SuspendUntil,
                svc.AutoTerminateEndOfCycle,
                svc.AutoTerminateReason));

        return Result<ServiceDetailDto>.Success(dto);
    }
}
