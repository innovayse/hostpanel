using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Resolves the one enabled identity plugin through <see cref="PluginActivator"/>, and refuses to choose when
/// more than one is enabled.
/// </summary>
public sealed class IdentityPluginResolver : IIdentityPluginResolver
{
    /// <summary>Turns each identity plugin into an instance once its settings allow it.</summary>
    private readonly PluginActivator _activator;

    /// <summary>Registry of plugins loaded at startup, read for the identity ones.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="IdentityPluginResolver"/> class.</summary>
    /// <param name="activator">Turns each identity plugin into an instance once its settings allow it.</param>
    /// <param name="registry">Registry of plugins loaded at startup, read for the identity ones.</param>
    public IdentityPluginResolver(PluginActivator activator, PluginRegistry registry)
    {
        _activator = activator;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<IClientResolver?> ResolveAsync(CancellationToken ct)
    {
        var enabled = new List<PluginActivation<IClientResolver>>();
        foreach (var plugin in _registry.OfType(PluginType.Identity))
        {
            var activation = await _activator.ActivateAsync<IClientResolver>(plugin, ct);

            // An enabled plugin that only lacks a setting still counts: the admin switched two on, and which
            // one answers must not depend on which of them happens to be fully configured today.
            if (activation.Outcome is PluginActivationOutcome.Activated or PluginActivationOutcome.RequiredSettingMissing)
            {
                enabled.Add(activation);
            }
        }

        if (enabled.Count > 1)
        {
            throw new IdentityPluginConflictException(enabled.Select(activation => activation.PluginId).ToList());
        }

        return enabled.SingleOrDefault()?.Instance;
    }
}
