using Hostpanel.Sdk.Plugins.Provisioning;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A hosting panel plugin that answers what the <see cref="FakePanelScript"/> of the call's server says and implements only the panel
/// contract, so it has no login capability. Built by the activator like a real plugin: it asks for its own configuration and for the
/// host's <see cref="IPanelTransport"/>, which proves the transport is injected.
/// </summary>
public class FakeLoginlessPanelPlugin : IProvisioningPanelPlugin
{
    /// <summary>The transport the host injected.</summary>
    private readonly IPanelTransport _transport;

    /// <summary>Initializes the plugin the way the activator does.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    /// <param name="transport">The host's transport, which a real plugin reaches its panel through.</param>
    public FakeLoginlessPanelPlugin(IConfiguration configuration, IPanelTransport transport)
    {
        _transport = transport;
    }

    /// <inheritdoc/>
    public Task<PanelCreateResult> CreateAccountAsync(PanelCreateAccountContext context, CancellationToken ct)
    {
        var script = FakePanelScript.For(context.Server);
        script.Note(context, _transport);
        return Task.FromResult(script.Create);
    }

    /// <inheritdoc/>
    public Task<PanelActionResult> SuspendAccountAsync(PanelSuspendContext context, CancellationToken ct)
    {
        return Task.FromResult(Answer(context.Server, context));
    }

    /// <inheritdoc/>
    public Task<PanelActionResult> UnsuspendAccountAsync(PanelAccountContext context, CancellationToken ct)
    {
        return Task.FromResult(Answer(context.Server, context));
    }

    /// <inheritdoc/>
    public Task<PanelActionResult> TerminateAccountAsync(PanelTerminateContext context, CancellationToken ct)
    {
        return Task.FromResult(Answer(context.Server, context));
    }

    /// <inheritdoc/>
    public Task<PanelActionResult> ChangePackageAsync(PanelChangePackageContext context, CancellationToken ct)
    {
        return Task.FromResult(Answer(context.Server, context));
    }

    /// <inheritdoc/>
    public Task<PanelActionResult> ChangePasswordAsync(PanelChangePasswordContext context, CancellationToken ct)
    {
        return Task.FromResult(Answer(context.Server, context));
    }

    /// <inheritdoc/>
    public Task<PanelSsoResult> CreateSsoLinkAsync(PanelSsoContext context, CancellationToken ct)
    {
        var script = FakePanelScript.For(context.Server);
        script.Note(context, _transport);
        return Task.FromResult(script.Sso);
    }

    /// <inheritdoc/>
    public Task<PanelUsageResult> GetUsageAsync(PanelAccountContext context, CancellationToken ct)
    {
        var script = FakePanelScript.For(context.Server);
        script.Note(context, _transport);
        return Task.FromResult(script.Usage);
    }

    /// <inheritdoc/>
    public Task<PanelConnectionTestResult> TestConnectionAsync(PanelTestConnectionContext context, CancellationToken ct)
    {
        var script = FakePanelScript.For(context.Server);
        script.Note(context, _transport);
        return Task.FromResult(script.Test);
    }

    /// <inheritdoc/>
    public Task<PanelAccountListResult> ListAccountsAsync(PanelListAccountsContext context, CancellationToken ct)
    {
        var script = FakePanelScript.For(context.Server);
        script.Note(context, _transport);
        return Task.FromResult(script.List(context.Cursor));
    }

    /// <summary>Records the context and answers with the script's one answer for every account action.</summary>
    /// <param name="server">The connection the context carries, which names the script.</param>
    /// <param name="context">The context the plugin received.</param>
    /// <returns>The scripted answer.</returns>
    /// <exception cref="Exception">Thrown when the script says every operation throws.</exception>
    private PanelActionResult Answer(PanelServerConnection server, object context)
    {
        var script = FakePanelScript.For(server);
        script.Note(context, _transport);
        return script.Action;
    }
}
