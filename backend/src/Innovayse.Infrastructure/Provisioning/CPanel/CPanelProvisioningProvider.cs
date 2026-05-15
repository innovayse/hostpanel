namespace Innovayse.Infrastructure.Provisioning.CPanel;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;
using Microsoft.Extensions.Options;

/// <summary>
/// cPanel WHM implementation of <see cref="IProvisioningProvider"/>.
/// Delegates all provisioning operations to <see cref="CPanelClient"/>.
/// </summary>
public sealed class CPanelProvisioningProvider(
    CPanelClient client,
    IOptions<CPanelSettings> options) : IProvisioningProvider
{
    /// <summary>Resolved cPanel configuration settings.</summary>
    private readonly CPanelSettings _settings = options.Value;

    /// <inheritdoc/>
    public async Task<ProvisioningResult> ProvisionAsync(ProvisionRequest req, CancellationToken ct)
    {
        await client.CreateAccountAsync(
            req.DomainName,
            req.Username,
            req.Password,
            req.Package,
            ct);

        return new ProvisioningResult(true, req.Username, null);
    }

    /// <inheritdoc/>
    public async Task SuspendAsync(SuspendRequest req, CancellationToken ct)
    {
        await client.SuspendAccountAsync(req.ProvisioningRef, req.Reason, ct);
    }

    /// <inheritdoc/>
    public async Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        await client.UnsuspendAccountAsync(provisioningRef, ct);
    }

    /// <inheritdoc/>
    public async Task TerminateAsync(TerminateRequest req, CancellationToken ct)
    {
        await client.RemoveAccountAsync(req.ProvisioningRef, ct);
    }

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        // cPanel does not expose stored passwords via the API.
        // Return the username as provisioningRef with a placeholder password.
        var credentials = new ServiceCredentials(
            provisioningRef,
            "**hidden**",
            $"{provisioningRef}.hosted",
            _settings.ServerIp,
            $"{_settings.ApiUrl.TrimEnd('/')}/cpanel");

        return Task.FromResult(credentials);
    }

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, CancellationToken ct)
    {
        return client.GetCPanelSsoUrlAsync(provisioningRef, ct);
    }
}
