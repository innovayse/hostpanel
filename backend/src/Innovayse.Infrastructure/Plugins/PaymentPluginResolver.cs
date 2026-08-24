namespace Innovayse.Infrastructure.Plugins;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves a loaded payment plugin into a ready-to-use <see cref="IPaymentPlugin"/> instance,
/// gated by admin-configured settings rather than static appsettings. Settings are read fresh
/// on every call so that credential edits made in the admin panel take effect immediately,
/// without an API restart.
/// </summary>
/// <param name="settings">Repository used to read the current settings snapshot.</param>
/// <param name="registry">Registry of plugins loaded at startup.</param>
/// <param name="serviceProvider">Service provider used to construct the plugin instance with its other dependencies.</param>
/// <param name="hostConfiguration">Host application configuration, layered underneath the settings-derived values.</param>
/// <param name="logger">Structured logger — a null result is ambiguous to callers, so each rejection gate logs why.</param>
public sealed class PaymentPluginResolver(
    ISettingRepository settings,
    PluginRegistry registry,
    IServiceProvider serviceProvider,
    IConfiguration hostConfiguration,
    ILogger<PaymentPluginResolver> logger) : IPaymentPluginResolver
{
    /// <inheritdoc/>
    public async Task<IPaymentPlugin?> ResolveAsync(string module, CancellationToken ct)
    {
        var plugin = registry.Find(module);
        if (plugin is null || plugin.Manifest.Type != PluginType.Payment)
        {
            logger.LogWarning(
                "Payment plugin '{Module}' is not installed or is not a payment-type plugin.", module);
            return null;
        }

        // Manifest.Type == Payment is only a JSON claim the plugin author made; it says nothing
        // about the CLR type actually loaded. Verify the implementation really implements
        // IPaymentPlugin before trusting it far enough to construct and cast it below — a
        // mistyped third-party manifest must return the documented null, not an
        // InvalidCastException thrown out of the payment path.
        if (!typeof(IPaymentPlugin).IsAssignableFrom(plugin.ImplementationType))
        {
            logger.LogWarning(
                "Payment plugin '{Module}' declares Type={ManifestType} but its implementation " +
                "type '{ImplementationType}' does not implement {InterfaceType}.",
                module, plugin.Manifest.Type, plugin.ImplementationType, nameof(IPaymentPlugin));
            return null;
        }

        var prefix = IntegrationSettingKeys.Prefix(module);
        var allSettings = await settings.ListAsync(ct);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in allSettings)
        {
            if (setting.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                values[setting.Key] = setting.Value;
            }
        }

        if (!values.TryGetValue(IntegrationSettingKeys.EnabledKey(module), out var isEnabled)
            || !string.Equals(isEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Payment plugin '{Module}' is disabled.", module);
            return null;
        }

        foreach (var field in plugin.Manifest.Fields)
        {
            if (!field.Required)
            {
                continue;
            }

            if (!values.TryGetValue(IntegrationSettingKeys.FieldKey(module, field.Key), out var fieldValue)
                || string.IsNullOrWhiteSpace(fieldValue))
            {
                logger.LogWarning(
                    "Payment plugin '{Module}' is missing required setting '{Field}'.", module, field.Key);
                return null;
            }
        }

        var composed = new ConfigurationBuilder()
            .AddConfiguration(hostConfiguration)
            .AddInMemoryCollection(values)
            .Build();

        return (IPaymentPlugin)ActivatorUtilities.CreateInstance(
            serviceProvider, plugin.ImplementationType, (IConfiguration)composed);
    }
}
