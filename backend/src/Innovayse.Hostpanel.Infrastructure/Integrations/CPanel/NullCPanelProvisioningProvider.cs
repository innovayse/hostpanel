using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;

/// <summary>
/// No-op cPanel provisioning provider used in development and testing.
/// Returns predictable results without calling any external cPanel/WHM API.
/// The real provider is <see cref="Whm.WhmProvisioningProvider"/>, built per server by the provisioning factory.
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
    public Task UnsuspendAsync(UnsuspendAccountOrder req, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: unsuspend ref={Ref}", req.ProvisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: terminate ref={Ref}", req.ProvisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(AccountCredentialsOrder req, CancellationToken ct)
    {
        var creds = new ServiceCredentials("devuser", "devpass", "dev.example.com", "127.0.0.1", "https://cpanel.dev.example.com");
        return Task.FromResult(creds);
    }

    /// <inheritdoc/>
    public Task<string> GetPanelSsoUrlAsync(PanelSsoOrder req, CancellationToken ct)
    {
        return Task.FromResult($"https://cpanel.dev.example.com/sso?ref={req.ProvisioningRef}");
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(ChangeAccountPasswordOrder req, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: change password ref={Ref}", req.ProvisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ChangePackageAsync(ChangeAccountPackageOrder req, CancellationToken ct)
    {
        _logger.LogInformation("NullCPanelProvisioningProvider: change package ref={Ref} → {Package}", req.ProvisioningRef, req.NewPackage);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="DomainRuleException">Always thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c>, like the real cPanel provider this stands in for.</exception>
    public Task<HostedAccountState> GetAccountAsync(AccountStateOrder req, CancellationToken ct)
    {
        throw new DomainRuleException(
            HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "The cPanel provider does not read a single account.");
    }

    /// <inheritdoc/>
    /// <exception cref="DomainRuleException">Always thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c>, like the real cPanel provider this stands in for.</exception>
    public Task<IReadOnlyList<string>> ListPackagesAsync(CancellationToken ct)
    {
        throw new DomainRuleException(
            HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "The cPanel provider does not list packages.");
    }
}
