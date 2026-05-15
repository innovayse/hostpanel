namespace Innovayse.Infrastructure.Services;

using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// No-op provisioning provider used in development and testing.
/// Returns a predictable reference without calling any external API.
/// Replace with <c>CpanelProvisioningProvider</c> for production.
/// </summary>
public sealed class NullProvisioningProvider(ILogger<NullProvisioningProvider> logger) : IProvisioningProvider
{
    /// <inheritdoc/>
    public Task<string> ProvisionAsync(int clientId, int productId, string billingCycle, CancellationToken ct)
    {
        var reference = $"null-{clientId}-{productId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        logger.LogInformation(
            "NullProvisioningProvider: provision client={ClientId} product={ProductId} cycle={Cycle} → {Ref}",
            clientId, productId, billingCycle, reference);
        return Task.FromResult(reference);
    }

    /// <inheritdoc/>
    public Task SuspendAsync(string provisioningRef, CancellationToken ct)
    {
        logger.LogInformation("NullProvisioningProvider: suspend ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        logger.LogInformation("NullProvisioningProvider: unsuspend ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(string provisioningRef, CancellationToken ct)
    {
        logger.LogInformation("NullProvisioningProvider: terminate ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }
}
