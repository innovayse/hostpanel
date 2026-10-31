using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Test stub for <see cref="IProvisioningProvider"/>.
/// Returns hardcoded success results without making any real network calls.
/// </summary>
public sealed class StubProvisioningProvider : IProvisioningProvider
{
    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
        => Task.FromResult(ProvisioningResult.Created("stub-user-123"));

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
        => Task.FromResult(new ServiceCredentials(
            "stub-user",
            "stub-pass",
            "stub.com",
            "1.2.3.4",
            "http://panel.stub.com"));

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct)
        => Task.FromResult("https://stub-panel.example.com/sso");

    /// <inheritdoc/>
    public Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
        => Task.CompletedTask;
}
