using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IProvisioningProvider"/> that records every call instead of reaching a real
/// hosting server, so a test can assert a domain event's provisioning side effect happened
/// exactly once. A fake port standing in for the hosting panel (v7)/control panel upstream, which a test must not call.
/// </summary>
public sealed class RecordingProvisioningProvider : IProvisioningProvider
{
    /// <summary>Every <see cref="SuspendAsync"/> call this fake was asked to make, in order.</summary>
    private readonly List<SuspendAccountOrder> _suspended = [];

    /// <summary>Every <see cref="UnsuspendAsync"/> reference this fake was asked to unsuspend, in order.</summary>
    private readonly List<string> _unsuspended = [];

    /// <summary>Every <see cref="TerminateAsync"/> call this fake was asked to make, in order.</summary>
    private readonly List<TerminateAccountOrder> _terminated = [];

    /// <summary>Every <see cref="SuspendAsync"/> call this fake was asked to make, in order.</summary>
    public IReadOnlyList<SuspendAccountOrder> Suspended => _suspended;

    /// <summary>Every provisioning reference this fake was asked to unsuspend, in order.</summary>
    public IReadOnlyList<string> Unsuspended => _unsuspended;

    /// <summary>Every <see cref="TerminateAsync"/> call this fake was asked to make, in order.</summary>
    public IReadOnlyList<TerminateAccountOrder> Terminated => _terminated;

    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct) =>
        Task.FromResult(ProvisioningResult.Created("recording-user-123"));

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        _suspended.Add(req);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        _unsuspended.Add(provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        _terminated.Add(req);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct) =>
        Task.FromResult(new ServiceCredentials(
            "recording-user", "recording-pass", "recording.example.test", "10.0.0.1", "http://panel.recording.example.test"));

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct) =>
        Task.FromResult("https://recording-panel.example.test/sso");

    /// <inheritdoc/>
    public Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct) =>
        Task.CompletedTask;
}
