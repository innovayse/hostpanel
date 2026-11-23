using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Results;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// <see cref="FakeLoginlessPanelPlugin"/> that also implements the login capability, answering what the
/// <see cref="FakePanelScript"/> of the call's server says.
/// </summary>
public sealed class FakePanelPlugin : FakeLoginlessPanelPlugin, IProvisioningPanelLoginPlugin
{
    /// <summary>The transport the host injected.</summary>
    private readonly IPanelTransport _transport;

    /// <summary>Initializes the plugin the way the activator does.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin.</param>
    /// <param name="transport">The host's transport, which a real plugin reaches its panel through.</param>
    public FakePanelPlugin(IConfiguration configuration, IPanelTransport transport)
        : base(configuration, transport)
    {
        _transport = transport;
    }

    /// <inheritdoc/>
    public Task<ValueResult<PanelLoginDetails, PanelRefusalReason>> GetLoginDetailsAsync(PanelAccountContext context, CancellationToken ct)
    {
        var script = FakePanelScript.For(context.Server);
        script.Note(context, _transport);
        return Task.FromResult(script.Login);
    }
}
