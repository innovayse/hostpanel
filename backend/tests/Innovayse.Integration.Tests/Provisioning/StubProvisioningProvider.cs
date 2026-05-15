namespace Innovayse.Integration.Tests.Provisioning;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;

/// <summary>
/// Test stub for <see cref="IProvisioningProvider"/>.
/// Returns hardcoded success results without making any real network calls.
/// </summary>
internal sealed class StubProvisioningProvider : IProvisioningProvider
{
    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionRequest req, CancellationToken ct)
        => Task.FromResult(new ProvisioningResult(true, "stub-user-123", null));

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendRequest req, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateRequest req, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
        => Task.FromResult(new ServiceCredentials(
            "stub-user",
            "stub-pass",
            "stub.com",
            "1.2.3.4",
            "http://cpanel.stub.com"));

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, CancellationToken ct)
        => Task.FromResult("https://stub-cpanel.example.com/sso");
}
