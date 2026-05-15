namespace Innovayse.Application.Services.Queries.GetService;

using Innovayse.Application.Services.DTOs;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns a single enriched client service for admin detail views.</summary>
public sealed class GetServiceHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="GetServiceQuery"/> by loading the service and resolving
    /// the product name and client name.
    /// </summary>
    /// <param name="qry">The query containing the service ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Enriched service detail DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task<ServiceDetailDto> HandleAsync(GetServiceQuery qry, CancellationToken ct)
    {
        var svc = await serviceRepo.FindByIdAsync(qry.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {qry.ServiceId} not found.");

        var product = await productRepo.FindByIdAsync(svc.ProductId, ct);
        var client = await clientRepo.FindByIdAsync(svc.ClientId, ct);

        var clientName = client is not null
            ? $"{client.FirstName} {client.LastName}"
            : "Unknown";

        return new ServiceDetailDto(
            svc.Id,
            svc.ClientId,
            clientName,
            svc.ProductId,
            product?.Name ?? "Unknown",
            svc.Domain,
            svc.DedicatedIp,
            svc.Username,
            svc.Password,
            svc.Quantity,
            svc.FirstPaymentAmount,
            svc.RecurringAmount,
            svc.PaymentMethod,
            svc.PromotionCode,
            svc.SubscriptionId,
            svc.BillingCycle,
            svc.Status.ToString(),
            svc.ProvisioningRef,
            svc.NextRenewalAt,
            svc.CreatedAt,
            svc.TerminatedAt,
            svc.OverrideAutoSuspend,
            svc.SuspendUntil,
            svc.AutoTerminateEndOfCycle,
            svc.AutoTerminateReason,
            svc.AdminNotes);
    }
}
