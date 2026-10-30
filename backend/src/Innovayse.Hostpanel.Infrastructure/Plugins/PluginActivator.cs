using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Activates a loaded plugin as an SDK contract, gated by the admin-configured settings rather than static
/// appsettings. Settings are read fresh on every call, so a credential edited or a plugin switched off in
/// the admin panel takes effect on the next call, without an API restart.
/// </summary>
/// <remarks>
/// <para>
/// The one place a loaded plugin becomes an instance. Every plugin resolver (payment, fulfilment, identity)
/// and the hosted plugin runner go through it, so the gates a plugin passes before its code runs (it really
/// implements the contract, the admin switched it on, its required settings are stored) and the
/// configuration it is handed are the same for every plugin type and cannot drift between copies.
/// </para>
/// <para>
/// A plugin sees only its own configuration: the host's <c>integration:{id}</c> section with the
/// admin-saved <c>integration:{id}:*</c> settings layered over it. Nothing else from the host's
/// configuration (connection strings, signing keys, other integrations) is handed over. The SDK's base
/// classes read <c>integration:{id}:{key}</c> by its full path, so the plugin receives a configuration
/// holding just that section under its full path rather than a relative <see cref="IConfigurationSection"/>,
/// which would make every such read miss.
/// </para>
/// </remarks>
public sealed class PluginActivator
{
    /// <summary>The data context, used to read the plugin's current settings rows.</summary>
    private readonly AppDbContext _db;

    /// <summary>Decrypts the stored secret fields before they reach the plugin.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>
    /// Service provider used to construct the plugin instance with its other dependencies. The plugin type is
    /// loaded from a folder at run time, so its constructor is only known then; that is why Wolverine takes this
    /// activator from the container rather than building it (see <c>MessagingRegistration</c>).
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Host application configuration; only the plugin's own section is ever read from it.</summary>
    private readonly IConfiguration _hostConfiguration;

    /// <summary>Structured logger: a misconfigured plugin is left out silently otherwise, so each such gate logs why.</summary>
    private readonly ILogger<PluginActivator> _logger;

    /// <summary>Initializes a new instance of the <see cref="PluginActivator"/> class.</summary>
    /// <param name="db">The data context, used to read the plugin's current settings rows.</param>
    /// <param name="cipher">Decrypts the stored secret fields before they reach the plugin.</param>
    /// <param name="serviceProvider">Service provider used to construct the plugin instance with its other dependencies.</param>
    /// <param name="hostConfiguration">Host application configuration; only the plugin's own section is ever read from it.</param>
    /// <param name="logger">Structured logger: a misconfigured plugin is left out silently otherwise, so each such gate logs why.</param>
    public PluginActivator(
        AppDbContext db,
        ISecretSettingCipher cipher,
        IServiceProvider serviceProvider,
        IConfiguration hostConfiguration,
        ILogger<PluginActivator> logger)
    {
        _db = db;
        _cipher = cipher;
        _serviceProvider = serviceProvider;
        _hostConfiguration = hostConfiguration;
        _logger = logger;
    }

    /// <summary>
    /// Activates <paramref name="plugin"/> as <typeparamref name="TContract"/> when its implementation
    /// implements the contract, the admin has enabled it and every required setting is stored.
    /// </summary>
    /// <typeparam name="TContract">The SDK contract the caller is going to call the plugin through.</typeparam>
    /// <param name="plugin">The loaded plugin, as the plugin loader registered it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The outcome and, when it is <see cref="PluginActivationOutcome.Activated"/>, the instance. A contract
    /// mismatch and a missing required setting are logged here as warnings; a disabled plugin is not, because
    /// for some callers that is the normal state and only the caller knows whether it is worth a line.
    /// </returns>
    /// <exception cref="InvalidOperationException">The plugin's constructor cannot be satisfied from the container.</exception>
    public async Task<PluginActivation<TContract>> ActivateAsync<TContract>(LoadedPlugin plugin, CancellationToken ct)
        where TContract : class
    {
        var id = plugin.Manifest.Id;

        // Manifest.Type is only a JSON claim the plugin author made; it says nothing about the CLR type
        // actually loaded. Verify the implementation really implements the contract before trusting it far
        // enough to construct and cast it below: a mistyped third-party manifest must leave the plugin out,
        // not throw an InvalidCastException out of the caller's path.
        if (!typeof(TContract).IsAssignableFrom(plugin.ImplementationType))
        {
            _logger.LogWarning(
                "Plugin '{Module}' declares Type={ManifestType} but its implementation type '{ImplementationType}' "
                    + "does not implement {InterfaceType}.",
                id, plugin.Manifest.Type, plugin.ImplementationType, typeof(TContract).Name);
            return new PluginActivation<TContract>(id, PluginActivationOutcome.ContractNotImplemented, null);
        }

        var values = await ReadConfigurationAsync(id, ct);

        if (!values.TryGetValue(IntegrationSettingKeys.EnabledKey(id), out var isEnabled)
            || !string.Equals(isEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return new PluginActivation<TContract>(id, PluginActivationOutcome.Disabled, null);
        }

        foreach (var field in plugin.Manifest.Fields)
        {
            if (!field.Required)
            {
                continue;
            }

            if (!values.TryGetValue(IntegrationSettingKeys.FieldKey(id, field.Key), out var fieldValue)
                || string.IsNullOrWhiteSpace(fieldValue))
            {
                _logger.LogWarning("Plugin '{Module}' is missing required setting '{Field}'.", id, field.Key);
                return new PluginActivation<TContract>(id, PluginActivationOutcome.RequiredSettingMissing, null);
            }
        }

        var composed = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var instance = (TContract)ActivatorUtilities.CreateInstance(
            _serviceProvider, plugin.ImplementationType, (IConfiguration)composed);

        return new PluginActivation<TContract>(id, PluginActivationOutcome.Activated, instance);
    }

    /// <summary>
    /// Builds the configuration values one plugin may see: the host's own section for it, with the plugin's
    /// settings rows, decrypted, layered over it.
    /// </summary>
    /// <param name="id">The plugin id whose section and rows are read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full-path keys (<c>integration:{id}:{key}</c>) to their values, compared case-insensitively.</returns>
    private async Task<Dictionary<string, string?>> ReadConfigurationAsync(string id, CancellationToken ct)
    {
        // The host's own section for this plugin, keys kept under their full path.
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in _hostConfiguration.GetSection(IntegrationSettingKeys.Section(id)).AsEnumerable())
        {
            if (value is not null)
            {
                values[key] = value;
            }
        }

        // Only this plugin's rows, filtered in SQL; read-only, so no tracking is needed.
        var prefix = IntegrationSettingKeys.Prefix(id);
        var settings = await _db.Settings.AsNoTracking()
            .Where(s => s.Key.StartsWith(prefix))
            .ToListAsync(ct);
        foreach (var setting in settings)
        {
            values[setting.Key] = _cipher.Reveal(setting.Value);
        }

        return values;
    }
}
