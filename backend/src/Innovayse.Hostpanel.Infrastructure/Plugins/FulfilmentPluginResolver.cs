using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Resolves every enabled fulfilment plugin through <see cref="PluginActivator"/>. A fulfilment plugin an admin
/// left off is a normal state, so it is skipped without a log line; a misconfigured one is logged by the activator.
/// </summary>
public sealed class FulfilmentPluginResolver : IFulfilmentPluginResolver
{
    /// <summary>Turns each fulfilment plugin into an instance once its settings allow it.</summary>
    private readonly PluginActivator _activator;

    /// <summary>Registry of plugins loaded at startup, read for the fulfilment ones.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="FulfilmentPluginResolver"/> class.</summary>
    /// <param name="activator">Turns each fulfilment plugin into an instance once its settings allow it.</param>
    /// <param name="registry">Registry of plugins loaded at startup, read for the fulfilment ones.</param>
    public FulfilmentPluginResolver(PluginActivator activator, PluginRegistry registry)
    {
        _activator = activator;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActiveFulfilmentPlugin>> ResolveAllAsync(CancellationToken ct)
    {
        var active = new List<ActiveFulfilmentPlugin>();
        foreach (var plugin in _registry.OfType(PluginType.Fulfilment))
        {
            var activation = await _activator.ActivateAsync<IFulfilmentPlugin>(plugin, ct);
            if (activation.Instance is not null)
            {
                active.Add(new ActiveFulfilmentPlugin(activation.PluginId, activation.Instance));
            }
        }

        return active;
    }
}
