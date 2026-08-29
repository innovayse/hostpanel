namespace Innovayse.Application.Provisioning.Commands.ChangePassword;

using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Application.Resources;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Localization;

/// <summary>
/// Changes the password of a provisioned hosting account by calling the provisioning provider.
/// </summary>
/// <param name="serviceRepo">Client service repository, for the account being changed.</param>
/// <param name="serverRepo">Server repository, for the server the account lives on.</param>
/// <param name="providerFactory">Factory creating the provisioning provider for that server.</param>
/// <param name="localizer">The refusal sentences, in the caller's own language.</param>
public sealed class ChangePasswordHandler(
    IClientServiceRepository serviceRepo,
    IServerRepository serverRepo,
    IProvisioningProviderFactory providerFactory,
    IStringLocalizer<ValidationMessages> localizer)
{
    /// <summary>
    /// Handles <see cref="ChangePasswordCommand"/>.
    /// </summary>
    /// <param name="cmd">The command containing the service identifier and new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found, has no provisioning reference, or has no server assigned.
    /// </exception>
    public async Task HandleAsync(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException(localizer["ClientServiceNotFound", cmd.ServiceId]);

        if (service.ProvisioningRef is null)
        {
            throw new InvalidOperationException(localizer["ServiceNoProvisioningReference", cmd.ServiceId]);
        }

        var server = (service.ServerId.HasValue
            ? await serverRepo.FindByIdAsync(service.ServerId.Value, ct)
            : null) ?? throw new InvalidOperationException(localizer["ServiceNoServerAssigned", cmd.ServiceId]);

        var provider = providerFactory.CreateFor(server);
        await provider.ChangePasswordAsync(service.ProvisioningRef, cmd.NewPassword, ct);
    }
}
