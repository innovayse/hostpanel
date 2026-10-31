using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A configurable <see cref="IProvisioningProvider"/> standing in for the real control-panel
/// provider a test must not call. Succeeds by default; setting <see cref="ShouldFail"/> makes
/// every member throw, the way an unreachable panel or a rejected request would.
/// </summary>
public sealed class FakeProvisioningProvider : IProvisioningProvider
{
    /// <summary>
    /// When <see langword="true"/>, every member throws instead of answering: the shape an
    /// upstream provider failure takes, whatever its real cause.
    /// </summary>
    public bool ShouldFail { get; set; }

    /// <summary>
    /// The answer <see cref="ProvisionAsync"/> gives when <see cref="ShouldFail"/> is not set; a
    /// created account by default. Set it to stage an existing account or a reference-less success.
    /// </summary>
    public ProvisioningResult ProvisionAnswer { get; set; } = ProvisioningResult.Created("fake-ref-123");

    /// <summary>How many times <see cref="ProvisionAsync"/> was called.</summary>
    public int ProvisionCalls { get; private set; }

    /// <summary>How many times <see cref="SuspendAsync"/> was called.</summary>
    public int SuspendCalls { get; private set; }

    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        ProvisionCalls++;
        return ShouldFail
            ? Task.FromResult(ProvisioningResult.Refused("fake provider refused to provision"))
            : Task.FromResult(ProvisionAnswer);
    }

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        SuspendCalls++;
        ThrowIfShouldFail();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        ThrowIfShouldFail();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        ThrowIfShouldFail();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        ThrowIfShouldFail();
        return Task.FromResult(new ServiceCredentials("fake-user", "fake-pass", "fake.example.com", "1.2.3.4", "https://panel.fake.example.com"));
    }

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct)
    {
        ThrowIfShouldFail();
        return Task.FromResult("https://fake-panel.example.com/sso");
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
    {
        ThrowIfShouldFail();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
    {
        ThrowIfShouldFail();
        return Task.CompletedTask;
    }

    /// <summary>Throws when <see cref="ShouldFail"/> is set, simulating an upstream refusal.</summary>
    private void ThrowIfShouldFail()
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("fake provider refused the request");
        }
    }
}
