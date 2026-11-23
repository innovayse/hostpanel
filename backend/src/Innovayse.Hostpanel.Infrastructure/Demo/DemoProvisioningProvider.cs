using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Server provisioning and control-panel access for the public demo: every operation succeeds at
/// once and nothing leaves the process. The provisioning reference is derived from the account
/// name, so provisioning the same account twice gives the same reference.
/// </summary>
public sealed class DemoProvisioningProvider : IProvisioningProvider
{
    /// <summary>The address the fake control panel claims to live at; reserved for documentation.</summary>
    private const string PanelUrl = "https://panel.demo-hosting.example/";

    /// <summary>The server address shown with the account credentials.</summary>
    private const string ServerIp = "192.0.2.10";

    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct) =>
        Task.FromResult(ProvisioningResult.Created($"demo-{req.Username}@{req.DomainName}"));

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task UnsuspendAsync(UnsuspendAccountOrder req, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(AccountCredentialsOrder req, CancellationToken ct) =>
        Task.FromResult(new ServiceCredentials("demo-user", "demo-password", "demo-site.example", ServerIp, PanelUrl));

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(ControlPanelSsoOrder req, CancellationToken ct) =>
        Task.FromResult($"{PanelUrl}sso?account={Uri.EscapeDataString(req.ProvisioningRef)}");

    /// <inheritdoc/>
    public Task ChangePasswordAsync(ChangeAccountPasswordOrder req, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ChangePackageAsync(ChangeAccountPackageOrder req, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<HostedAccountState> GetAccountAsync(AccountStateOrder req, CancellationToken ct) =>
        Task.FromResult(new HostedAccountState(HostedAccountStatus.Active, null, null, ServerIp));
}
