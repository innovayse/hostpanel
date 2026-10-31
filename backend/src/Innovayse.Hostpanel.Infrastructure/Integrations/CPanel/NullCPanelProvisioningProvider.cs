using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;

/// <summary>
/// No-op cPanel provisioning provider used in development and testing.
/// Returns predictable results without calling any external cPanel/WHM API.
/// Replace with <see cref="CPanelProvisioningProvider"/> when a real server is configured.
/// </summary>
public sealed class NullCPanelProvisioningProvider : IProvisioningProvider
{
    /// <summary>Logger instance.</summary>
    private readonly ILogger<NullCPanelProvisioningProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="NullCPanelProvisioningProvider"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    public NullCPanelProvisioningProvider(ILogger<NullCPanelProvisioningProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        var provRef = $"dev-{req.Username}@{req.DomainName}";
        _logger.LogInformation(
            "NullCPanelProvisioningProvider: provisioned service {ServiceId} → domain={Domain} user={Username} ref={Ref}",
            req.ServiceId, req.DomainName, req.Username, provRef);
        return Task.FromResult(ProvisioningResult.Created(provRef));
    }

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: suspend ref={Ref}", req.ProvisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: unsuspend ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: terminate ref={Ref}", req.ProvisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        var creds = new ServiceCredentials("devuser", "devpass", "dev.example.com", "127.0.0.1", "https://cpanel.dev.example.com");
        return Task.FromResult(creds);
    }

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct)
    {
        return Task.FromResult($"https://cpanel.dev.example.com/sso?ref={provisioningRef}");
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: change password ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: change package ref={Ref} → {Package}", provisioningRef, newPackage);
        return Task.CompletedTask;
    }
}
