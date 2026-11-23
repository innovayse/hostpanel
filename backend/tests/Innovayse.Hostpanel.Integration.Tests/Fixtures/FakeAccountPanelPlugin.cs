using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Results;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// <see cref="FakeLoginlessPanelPlugin"/> that also reads one account, answering what the <see cref="FakePanelScript"/> of the call's
/// server says, so a test drives the sync against a plugin that declares <c>provisioningPanelAccount</c>.
/// </summary>
public sealed class FakeAccountPanelPlugin : FakeLoginlessPanelPlugin, IProvisioningPanelAccountPlugin
{
    /// <summary>The transport the host injected.</summary>
    private readonly IPanelTransport _transport;

    /// <summary>Initializes the plugin the way the activator does.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin.</param>
    /// <param name="transport">The host's transport, which a real plugin reaches its panel through.</param>
    public FakeAccountPanelPlugin(IConfiguration configuration, IPanelTransport transport)
        : base(configuration, transport)
    {
        _transport = transport;
    }

    /// <inheritdoc/>
    public Task<ValueResult<PanelAccount, PanelRefusalReason>> GetAccountAsync(PanelAccountContext context, CancellationToken ct)
    {
        var script = FakePanelScript.For(context.Server);
        script.Note(context, _transport);
        return Task.FromResult(script.Account);
    }
}
