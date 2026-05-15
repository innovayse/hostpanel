namespace Innovayse.Application.Services.Commands.UpdateService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="UpdateServiceCommand"/>.
/// Loads the service aggregate, applies editable field changes,
/// and handles status transitions.
/// </summary>
public sealed class UpdateServiceHandler(
    IClientServiceRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Updates the service's editable fields and optionally changes its status.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task HandleAsync(UpdateServiceCommand cmd, CancellationToken ct)
    {
        var service = await repo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        service.Update(
            cmd.Domain,
            cmd.DedicatedIp,
            cmd.Username,
            cmd.Password,
            cmd.BillingCycle,
            cmd.RecurringAmount,
            cmd.PaymentMethod,
            cmd.NextRenewalAt,
            cmd.SubscriptionId,
            cmd.OverrideAutoSuspend,
            cmd.SuspendUntil,
            cmd.AutoTerminateEndOfCycle,
            cmd.AutoTerminateReason,
            cmd.AdminNotes,
            cmd.ProvisioningRef,
            cmd.FirstPaymentAmount,
            cmd.PromotionCode,
            cmd.TerminatedAt);

        if (cmd.Status is not null)
        {
            if (!Enum.TryParse<ServiceStatus>(cmd.Status, ignoreCase: true, out var newStatus))
                throw new InvalidOperationException($"Invalid service status: '{cmd.Status}'.");

            if (newStatus != service.Status)
            {
                switch (newStatus)
                {
                    case ServiceStatus.Active:
                        service.Unsuspend();
                        break;
                    case ServiceStatus.Suspended:
                        service.Suspend();
                        break;
                    case ServiceStatus.Terminated:
                        service.Terminate();
                        break;
                    case ServiceStatus.Pending:
                        // Pending is set only on creation — no dedicated domain method
                        break;
                }
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
