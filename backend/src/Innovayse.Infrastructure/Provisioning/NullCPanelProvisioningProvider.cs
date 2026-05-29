namespace Innovayse.Infrastructure.Provisioning;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// No-op cPanel provisioning provider used in development and testing.
/// Returns predictable results without calling any external cPanel/WHM API.
/// Replace with <see cref="CPanel.CPanelProvisioningProvider"/> when a real server is configured.
/// </summary>
/// <param name="logger">Logger instance.</param>
public sealed class NullCPanelProvisioningProvider(ILogger<NullCPanelProvisioningProvider> logger) : IProvisioningProvider
{
    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionRequest req, CancellationToken ct)
    {
        var provRef = $"dev-{req.Username}@{req.DomainName}";
        logger.LogInformation(
            "NullCPanelProvisioningProvider: provisioned service {ServiceId} → domain={Domain} user={Username} ref={Ref}",
            req.ServiceId, req.DomainName, req.Username, provRef);
        return Task.FromResult(new ProvisioningResult(true, provRef, null));
    }

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendRequest req, CancellationToken ct)
    {
        logger.LogInformation("NullCPanelProvisioningProvider: suspend ref={Ref}", req.ProvisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        logger.LogInformation("NullCPanelProvisioningProvider: unsuspend ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateRequest req, CancellationToken ct)
    {
        logger.LogInformation("NullCPanelProvisioningProvider: terminate ref={Ref}", req.ProvisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        var creds = new ServiceCredentials("devuser", "devpass", "dev.example.com", "127.0.0.1", "https://cpanel.dev.example.com");
        return Task.FromResult(creds);
    }

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, CancellationToken ct)
    {
        return Task.FromResult($"https://cpanel.dev.example.com/sso?ref={provisioningRef}");
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
    {
        logger.LogInformation("NullCPanelProvisioningProvider: change password ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
    {
        logger.LogInformation("NullCPanelProvisioningProvider: change package ref={Ref} → {Package}", provisioningRef, newPackage);
        return Task.CompletedTask;
    }
}
