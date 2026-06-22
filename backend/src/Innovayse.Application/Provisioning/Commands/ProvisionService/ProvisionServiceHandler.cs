namespace Innovayse.Application.Provisioning.Commands.ProvisionService;

using System.Security.Cryptography;
using Innovayse.Application.Common;
using Innovayse.Application.Servers;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Provisions a pending hosting service by selecting the best server,
/// calling the appropriate provisioning provider, activating the service,
/// and persisting all changes.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="productRepo">Product repository to look up package name.</param>
/// <param name="providerFactory">Factory to create per-server provisioning providers.</param>
/// <param name="serverSelector">Selects the optimal server using proportional fill strategy.</param>
/// <param name="unitOfWork">Unit of work for persistence.</param>
public sealed class ProvisionServiceHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IProvisioningProviderFactory providerFactory,
    IServerSelector serverSelector,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Handles <see cref="ProvisionServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The provision command containing the service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service is not found, no server is available, or provisioning fails.
    /// </exception>
    public async Task HandleAsync(ProvisionServiceCommand cmd, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {cmd.ServiceId} not found.");

        // Look up the product to get the hosting package name and server group
        var product = await productRepo.FindByIdAsync(service.ProductId, ct);

        // Select the best server — prefer the product's server group, fall back to module-level selection
        var server = (product?.ServerGroupId is { } sgId
            ? await serverSelector.SelectFromGroupAsync(sgId, ct)
            : await serverSelector.SelectAsync(ServerModule.Cwp7, ct)) ?? throw new InvalidOperationException("No eligible server available for provisioning.");

        var provider = providerFactory.CreateFor(server);

        var request = new ProvisionRequest(
            service.Id,
            service.Domain ?? $"temp{service.Id}.example.com",
            service.Username ?? $"user{service.Id}",
            GeneratePassword(),
            product?.PackageName ?? "default");

        var result = await provider.ProvisionAsync(request, ct);

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
