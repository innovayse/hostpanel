namespace Innovayse.Application.Services.Commands.SetupService;

using Innovayse.Application.Common;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Handles <see cref="SetupServiceCommand"/> by updating the service with
/// client-provided hosting details and triggering provisioning.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="provisioningProvider">Provisioning provider (cPanel, etc.).</param>
/// <param name="unitOfWork">Unit of work for persisting changes.</param>
public sealed class SetupServiceHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Sets up the service with the provided domain, username, and password,
    /// then provisions it via the configured provider.
    /// </summary>
    /// <param name="cmd">The setup command with hosting details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found, not in Pending status, or provisioning fails.
    /// </exception>
    public async Task HandleAsync(SetupServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.Status is not ServiceStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot set up a service with status {service.Status}. Only Pending services can be set up.");
        }

        var request = new ProvisionRequest(
            service.Id,
            cmd.Domain,
            cmd.Username,
            cmd.Password,
            "default");

        var result = await provisioningProvider.ProvisionAsync(request, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Provisioning failed for service {cmd.ServiceId}: {result.ErrorMessage}");
        }

        // Save the client-provided hosting details before activating
        service.Update(
            domain: cmd.Domain,
            dedicatedIp: service.DedicatedIp,
            username: cmd.Username,
            password: cmd.Password,
            billingCycle: service.BillingCycle,
            recurringAmount: service.RecurringAmount,
            paymentMethod: service.PaymentMethod,
            nextRenewalAt: service.NextRenewalAt,
            subscriptionId: service.SubscriptionId,
            overrideAutoSuspend: service.OverrideAutoSuspend,
            suspendUntil: service.SuspendUntil,
            autoTerminateEndOfCycle: service.AutoTerminateEndOfCycle,
            autoTerminateReason: service.AutoTerminateReason,
            adminNotes: service.AdminNotes,
            provisioningRef: service.ProvisioningRef,
            firstPaymentAmount: service.FirstPaymentAmount,
            promotionCode: service.PromotionCode,
            terminatedAt: service.TerminatedAt,
            serverId: service.ServerId,
            quantity: service.Quantity,
            productId: null);

        service.Activate(result.ProvisioningRef!);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
