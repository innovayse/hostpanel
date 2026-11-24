using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// <see cref="IProvisioningProvider"/> backed by an installed hosting panel plugin, so every handler that provisions reaches a panel
/// through the one port whether the panel is driven by the host or by a plugin. Each call is one contract call through
/// <see cref="PanelPluginCaller"/>; what the plugin answers is mapped onto the port by <see cref="PanelOutcomeExtensions"/>: an
/// operation that was done, was already done or was accepted returns normally, and every other answer is a refusal with a panel code.
/// </summary>
public sealed class PanelPluginProvisioningProvider : IProvisioningProvider
{
    /// <summary>The plugin that serves the server's module.</summary>
    private readonly string _pluginId;

    /// <summary>The server every call is made for.</summary>
    private readonly ServerConnectionDto _server;

    /// <summary>Activates the plugin and runs one call on it.</summary>
    private readonly PanelPluginCaller _caller;

    /// <summary>Builds the connection the plugin receives, reading the server's stored password.</summary>
    private readonly PanelConnectionReader _connections;

    /// <summary>Gives the plugin the opaque reference it sees for a client.</summary>
    private readonly IPluginReferences _references;

    /// <summary>Records an accepted but unfinished operation.</summary>
    private readonly ILogger<PanelPluginProvisioningProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="PanelPluginProvisioningProvider"/> class.</summary>
    /// <param name="pluginId">The plugin that serves the server's module.</param>
    /// <param name="server">The server every call is made for.</param>
    /// <param name="caller">Activates the plugin and runs one call on it.</param>
    /// <param name="connections">Builds the connection the plugin receives, reading the server's stored password.</param>
    /// <param name="references">Gives the plugin the opaque reference it sees for a client.</param>
    /// <param name="logger">Records an accepted but unfinished operation.</param>
    public PanelPluginProvisioningProvider(
        string pluginId,
        ServerConnectionDto server,
        PanelPluginCaller caller,
        PanelConnectionReader connections,
        IPluginReferences references,
        ILogger<PanelPluginProvisioningProvider> logger)
    {
        _pluginId = pluginId;
        _server = server;
        _caller = caller;
        _connections = connections;
        _references = references;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <exception cref="DomainRuleException">
    /// Thrown with the panel's refusal code when the plugin refuses with a reason, <c>PANEL_PLUGIN_UNAVAILABLE</c> when it cannot be
    /// activated, and <c>PROVISIONING_OUTCOME_UNKNOWN</c> when it throws: the account may exist, and the retry adopts it.
    /// </exception>
    public async Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        PanelCreateResult result;
        try
        {
            result = await RunOnPanelAsync<IProvisioningPanelPlugin, PanelCreateResult>(
                nameof(IProvisioningPanelPlugin.CreateAccountAsync),
                (plugin, server, token) => plugin.CreateAccountAsync(
                    new PanelCreateAccountContext(
                        server,
                        new PanelService(req.ServiceId, null),
                        new PanelClientRef(_references.ForClient(_pluginId, req.ClientId), req.ClientEmail, req.ClientName),
                        new PanelPackage(req.Package),
                        req.DomainName,
                        req.Username,
                        req.Password),
                    token),
                ct);
        }
        catch (PanelPluginCallException)
        {
            throw new DomainRuleException(
                HostpanelErrorCodes.PROVISIONING_OUTCOME_UNKNOWN, "The panel plugin failed while creating the account.");
        }

        if (result.Outcome is PanelOperationOutcome.Refused)
        {
            throw new DomainRuleException(result.Reason.ToErrorCode(), "The control panel refused to create the account.");
        }

        // A success, an account that already exists and an account still being created all name the account: without a
        // reference no later suspend, unsuspend or terminate could find it, so that answer is a failure.
        if (result.Outcome is not (PanelOperationOutcome.Succeeded or PanelOperationOutcome.AlreadyApplied or PanelOperationOutcome.Pending)
            || string.IsNullOrWhiteSpace(result.ProvisioningRef))
        {
            return ProvisioningResult.Refused($"The panel plugin answered {result.Outcome} without an account the host can use.");
        }

        // A panel that cannot name the domain of the account it already holds (the answer to a retry after a pending create, for one)
        // leaves it to the host's own record: the service's domain is the only one this call asked it to create the account for.
        return result.Outcome switch
        {
            PanelOperationOutcome.AlreadyApplied => ProvisioningResult.AlreadyExists(result.ProvisioningRef, result.ExistingDomain ?? req.DomainName),
            PanelOperationOutcome.Pending => ProvisioningResult.Pending(result.ProvisioningRef),
            _ => ProvisioningResult.Created(result.ProvisioningRef),
        };
    }

    /// <inheritdoc/>
    public async Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        await RunActionAsync(
            nameof(IProvisioningPanelPlugin.SuspendAccountAsync),
            req.ServiceId,
            (plugin, server, token) => plugin.SuspendAccountAsync(
                new PanelSuspendContext(server, new PanelService(req.ServiceId, req.ProvisioningRef), req.Reason), token),
            ct);
    }

    /// <inheritdoc/>
    public async Task UnsuspendAsync(UnsuspendAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        await RunActionAsync(
            nameof(IProvisioningPanelPlugin.UnsuspendAccountAsync),
            req.ServiceId,
            (plugin, server, token) => plugin.UnsuspendAccountAsync(
                new PanelAccountContext(server, new PanelService(req.ServiceId, req.ProvisioningRef)), token),
            ct);
    }

    /// <inheritdoc/>
    public async Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var result = await RunOnPanelAsync<IProvisioningPanelPlugin, PanelActionResult>(
            nameof(IProvisioningPanelPlugin.TerminateAccountAsync),
            (plugin, server, token) => plugin.TerminateAccountAsync(
                new PanelTerminateContext(server, new PanelService(req.ServiceId, req.ProvisioningRef), req.Reason), token),
            ct);

        // An account the panel does not have is gone, which is what terminating asked for, so a retry is safe.
        if (result.Outcome is PanelOperationOutcome.NotFound)
        {
            _logger.LogInformation("Terminating service {ServiceId}: the panel has no such account, so it is already gone.", req.ServiceId);
            return;
        }

        Finish(result.Outcome, result.Reason, nameof(IProvisioningPanelPlugin.TerminateAccountAsync), req.ServiceId);
    }

    /// <inheritdoc/>
    /// <exception cref="DomainRuleException">
    /// Thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the plugin does not declare the login capability, and with the
    /// panel's refusal code when it refuses.
    /// </exception>
    public async Task<ServiceCredentials> GetCredentialsAsync(AccountCredentialsOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (!_caller.Declares(_pluginId, PluginCapabilities.ProvisioningPanelLogin))
        {
            throw new DomainRuleException(
                HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "The panel plugin does not declare the login capability.");
        }

        var result = await RunOnPanelAsync<IProvisioningPanelLoginPlugin, ValueResult<PanelLoginDetails, PanelRefusalReason>>(
            nameof(IProvisioningPanelLoginPlugin.GetLoginDetailsAsync),
            (plugin, server, token) => plugin.GetLoginDetailsAsync(
                new PanelAccountContext(server, new PanelService(req.ServiceId, req.ProvisioningRef)), token),
            ct);

        result.Outcome.EnsureDone(result.Reason);
        if (result.Value is not { } details)
        {
            throw new DomainRuleException(
                HostpanelErrorCodes.SERVICE_CREDENTIALS_UPSTREAM_FAILED, "The panel plugin answered success without login details.");
        }

        return new ServiceCredentials(
            details.Username, details.Password, details.Domain ?? string.Empty, details.ServerIp ?? _server.IpAddress ?? _server.Hostname, details.LoginUrl);
    }

    /// <inheritdoc/>
    /// <exception cref="DomainRuleException">
    /// Thrown with <c>SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED</c> when the plugin answers an address that is not an absolute http or
    /// https address, and with the panel's refusal code when it refuses.
    /// </exception>
    public async Task<string> GetCPanelSsoUrlAsync(ControlPanelSsoOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var result = await RunOnPanelAsync<IProvisioningPanelPlugin, PanelSsoResult>(
            nameof(IProvisioningPanelPlugin.CreateSsoLinkAsync),
            (plugin, server, token) => plugin.CreateSsoLinkAsync(
                new PanelSsoContext(server, new PanelService(req.ServiceId, req.ProvisioningRef), req.CallerIp), token),
            ct);

        result.Outcome.EnsureDone(result.Reason);

        // The link is handed to a browser, so only a web address passes; nothing of a rejected one is logged or returned.
        if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var link)
            || (link.Scheme != Uri.UriSchemeHttps && link.Scheme != Uri.UriSchemeHttp))
        {
            throw new DomainRuleException(
                HostpanelErrorCodes.SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED, "The panel plugin answered a sign in link that is not a web address.");
        }

        return link.AbsoluteUri;
    }

    /// <inheritdoc/>
    public async Task ChangePasswordAsync(ChangeAccountPasswordOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        await RunActionAsync(
            nameof(IProvisioningPanelPlugin.ChangePasswordAsync),
            req.ServiceId,
            (plugin, server, token) => plugin.ChangePasswordAsync(
                new PanelChangePasswordContext(server, new PanelService(req.ServiceId, req.ProvisioningRef), req.NewPassword), token),
            ct);
    }

    /// <inheritdoc/>
    public async Task ChangePackageAsync(ChangeAccountPackageOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        await RunActionAsync(
            nameof(IProvisioningPanelPlugin.ChangePackageAsync),
            req.ServiceId,
            (plugin, server, token) => plugin.ChangePackageAsync(
                new PanelChangePackageContext(server, new PanelService(req.ServiceId, req.ProvisioningRef), new PanelPackage(req.NewPackage)), token),
            ct);
    }

    /// <inheritdoc/>
    /// <exception cref="DomainRuleException">
    /// Thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the plugin does not declare the single account read, with
    /// <c>PANEL_ACCOUNT_NOT_FOUND</c> when the panel holds no such account, with the panel's refusal code when it refuses, and with
    /// <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> when it answers success without an account.
    /// </exception>
    public async Task<HostedAccountState> GetAccountAsync(AccountStateOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (!_caller.Declares(_pluginId, PluginCapabilities.ProvisioningPanelAccount))
        {
            throw new DomainRuleException(
                HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "The panel plugin does not declare the single account read.");
        }

        var result = await RunOnPanelAsync<IProvisioningPanelAccountPlugin, ValueResult<PanelAccount, PanelRefusalReason>>(
            nameof(IProvisioningPanelAccountPlugin.GetAccountAsync),
            (plugin, server, token) => plugin.GetAccountAsync(
                new PanelAccountContext(server, new PanelService(req.ServiceId, req.ProvisioningRef)), token),
            ct);

        result.Outcome.EnsureDone(result.Reason);
        if (result.Value is not { } account)
        {
            throw new DomainRuleException(
                HostpanelErrorCodes.SERVICE_PANEL_SYNC_UPSTREAM_FAILED, "The panel plugin answered success without an account.");
        }

        return new HostedAccountState(account.Status.ToHostedAccountStatus(), account.Domain, account.PackageName, account.IpAddress);
    }

    /// <inheritdoc/>
    /// <exception cref="DomainRuleException">
    /// Thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the plugin does not declare the package listing or says it cannot list, and
    /// with the panel's refusal code when it refuses.
    /// </exception>
    public async Task<IReadOnlyList<string>> ListPackagesAsync(CancellationToken ct)
    {
        if (!_caller.Declares(_pluginId, PluginCapabilities.ProvisioningPanelPackages))
        {
            throw new DomainRuleException(
                HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "The panel plugin does not declare the package listing.");
        }

        var result = await RunOnPanelAsync<IProvisioningPanelPackagesPlugin, ValueResult<IReadOnlyList<PanelPackageDetails>, PanelRefusalReason>>(
            nameof(IProvisioningPanelPackagesPlugin.ListPackagesAsync),
            (plugin, server, token) => plugin.ListPackagesAsync(new PanelListPackagesContext(server), token),
            ct);

        result.Outcome.EnsureDone(result.Reason);
        return (result.Value ?? []).Select(package => package.Name).ToList();
    }

    /// <summary>
    /// Runs one call on the plugin. The server's stored password is read only once the plugin is resolved, available and inside its
    /// time limit, so a plugin that is off, missing or broken never costs a read of it and never sees it.
    /// </summary>
    /// <typeparam name="TContract">The contract the call needs.</typeparam>
    /// <typeparam name="TResult">What the call answers.</typeparam>
    /// <param name="operation">The contract method name, for the log.</param>
    /// <param name="call">The call on the activated plugin, given the connection to put in its context and the token it must honour.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the plugin answered.</returns>
    private Task<TResult> RunOnPanelAsync<TContract, TResult>(
        string operation, Func<TContract, PanelServerConnection, CancellationToken, Task<TResult>> call, CancellationToken ct)
        where TContract : class
    {
        return _caller.RunAsync<TContract, TResult>(
            _pluginId, operation, async (plugin, token) => await call(plugin, await _connections.ReadAsync(_server, token), token), ct);
    }

    /// <summary>Runs one operation that answers with a <see cref="PanelActionResult"/> and maps the answer onto the port.</summary>
    /// <param name="operation">The contract method name, for the log.</param>
    /// <param name="serviceId">The service the operation is for, for the log.</param>
    /// <param name="call">The call on the activated plugin, given the connection to put in its context and the token it must honour.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the plugin did the work, found it done or accepted it.</returns>
    private async Task RunActionAsync(
        string operation,
        int serviceId,
        Func<IProvisioningPanelPlugin, PanelServerConnection, CancellationToken, Task<PanelActionResult>> call,
        CancellationToken ct)
    {
        var result = await RunOnPanelAsync(operation, call, ct);
        Finish(result.Outcome, result.Reason, operation, serviceId);
    }

    /// <summary>
    /// Maps one answer onto the port (see <see cref="PanelOutcomeExtensions.EnsureDone"/>) and records an accepted but unfinished
    /// operation. The platform already holds the state the caller wanted and nothing waits on the panel, so
    /// <see cref="PanelOperationOutcome.Pending"/> is success here; only creating an account has follow up state.
    /// </summary>
    /// <param name="outcome">How the plugin answered.</param>
    /// <param name="reason">The refusal reason, when it refused.</param>
    /// <param name="operation">The contract method name, for the log.</param>
    /// <param name="serviceId">The service the operation is for, for the log.</param>
    private void Finish(PanelOperationOutcome outcome, PanelRefusalReason? reason, string operation, int serviceId)
    {
        outcome.EnsureDone(reason);
        if (outcome is PanelOperationOutcome.Pending)
        {
            _logger.LogInformation("{Operation} for service {ServiceId} was accepted by the panel and is still being applied.", operation, serviceId);
        }
    }
}
