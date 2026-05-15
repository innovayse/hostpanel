namespace Innovayse.Application.Provisioning.Commands.ProvisionService;

using System.Security.Cryptography;
using Innovayse.Application.Common;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Services.Interfaces;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Provisions a pending hosting service by calling the configured provisioning provider,
/// activating the service aggregate, and persisting all changes.
/// </summary>
public sealed class ProvisionServiceHandler(
    IClientServiceRepository serviceRepo,
    IProvisioningProvider provisioningProvider,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Handles <see cref="ProvisionServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The provision command containing the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found or provisioning fails on the provider.
    /// </exception>
    public async Task HandleAsync(ProvisionServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        var request = new ProvisionRequest(
            service.Id,
            service.ProvisioningRef ?? $"temp{service.Id}.example.com",
            $"user{service.Id}",
            GeneratePassword(),
            "default");

        var result = await provisioningProvider.ProvisionAsync(request, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Provisioning failed for service {cmd.ServiceId}: {result.ErrorMessage}");
        }

        service.Activate(result.ProvisioningRef!);

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Generates a cryptographically random password for the new hosting account.</summary>
    /// <returns>A 16-character alphanumeric password string.</returns>
    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToBase64String(bytes).Replace("=", string.Empty, StringComparison.Ordinal)[..16];
    }
}
