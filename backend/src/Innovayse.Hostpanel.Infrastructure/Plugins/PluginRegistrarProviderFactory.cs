using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Resolves the provider that answers for a registrar module, which is the id of a registrar plugin. The plugin goes through
/// <see cref="PluginActivator"/> like every other plugin (switched on, configured, no pending migration); a module whose plugin
/// fails any gate resolves to <see cref="UnavailableRegistrarProvider"/>, which refuses with <c>REGISTRAR_NOT_CONFIGURED</c>.
/// </summary>
public sealed class PluginRegistrarProviderFactory : IRegistrarProviderFactory
{
    /// <summary>The plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Checks the activation gates and builds the plugin.</summary>
    private readonly PluginActivator _activator;

    /// <summary>Records why a module resolved to no provider, by plugin id only.</summary>
    private readonly ILogger<PluginRegistrarProviderFactory> _logger;

    /// <summary>Handed to each provider the factory builds.</summary>
    private readonly ILogger<PluginRegistrarProvider> _providerLogger;

    /// <summary>Initializes a new instance of the <see cref="PluginRegistrarProviderFactory"/> class.</summary>
    /// <param name="registry">The plugins loaded at startup.</param>
    /// <param name="activator">Checks the activation gates and builds the plugin.</param>
    /// <param name="logger">Records why a module resolved to no provider, by plugin id only.</param>
    /// <param name="providerLogger">Handed to each provider the factory builds.</param>
    public PluginRegistrarProviderFactory(
        PluginRegistry registry,
        PluginActivator activator,
        ILogger<PluginRegistrarProviderFactory> logger,
        ILogger<PluginRegistrarProvider> providerLogger)
    {
        _registry = registry;
        _activator = activator;
        _logger = logger;
        _providerLogger = providerLogger;
    }

    /// <inheritdoc />
    public async Task<IRegistrarProvider> GetProviderAsync(string? module, CancellationToken ct)
    {
        if (module is null)
        {
            return new UnavailableRegistrarProvider();
        }

        var plugin = _registry.FindRegistrar(module);
        if (plugin is null || !plugin.Manifest.Declares(PluginCapabilities.RegistrarOperations))
        {
            // A marker-only registrar plugin has nothing to call, which is the same as not being installed.
            _logger.LogWarning("Registrar module '{Module}' names no installed registrar plugin that offers operations.", module);
            return new UnavailableRegistrarProvider();
        }

        var prepared = await _activator.PrepareAsync<IRegistrarOperationsPlugin>(plugin, ct);
        if (prepared.Outcome != PluginActivationOutcome.Activated || prepared.Create is null || prepared.Configuration is null)
        {
            _logger.LogInformation(
                "Registrar plugin '{PluginId}' is not usable: {Outcome}.", prepared.PluginId, prepared.Outcome);
            return new UnavailableRegistrarProvider();
        }

        return new PluginRegistrarProvider(
            plugin.Manifest,
            prepared.Create(),
            RegistrarConnectionBuilder.Build(plugin.Manifest, prepared.Configuration),
            _providerLogger);
    }
}
