namespace Innovayse.Application.Provisioning.Commands.ChangePassword;

using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Changes the password of a provisioned hosting account by calling the provisioning provider.
/// </summary>
public sealed class ChangePasswordHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider)
{
    /// <summary>
    /// Handles <see cref="ChangePasswordCommand"/>.
    /// </summary>
    /// <param name="cmd">The command containing the service identifier and new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or has no provisioning reference.
    /// </exception>
    public async Task HandleAsync(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException($"ClientService {cmd.ServiceId} has no provisioning reference.");
        }

        await provisioningProvider.ChangePasswordAsync(service.ProvisioningRef, cmd.NewPassword, ct);
    }
}
