namespace Innovayse.Application.Admin.Integrations.Queries.ListIntegrations;

using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="ListIntegrationsQuery"/> by reading all integration-prefixed settings
/// and computing the enabled/configured status for each of the 10 known integrations,
/// then merging in any plugins registered via <see cref="IPluginIntegrationRegistry"/>.
/// </summary>
/// <param name="settings">Setting repository for key-value lookups.</param>
/// <param name="pluginRegistry">Plugin integration registry for discovering plugin-provided integrations.</param>
public sealed class ListIntegrationsHandler(ISettingRepository settings, IPluginIntegrationRegistry pluginRegistry)
{
    /// <summary>
    /// Static metadata for every integration: display name, category,
    /// required credential fields, and the full set of persisted fields.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Category, string[] RequiredFields, string[] AllFields)> _meta = new()
    {
        ["stripe"] = ("Stripe", "Payment Gateways", ["secret_key"], ["secret_key", "publishable_key", "webhook_secret", "mode"]),
        ["paypal"] = ("PayPal", "Payment Gateways", ["client_id", "client_secret"], ["client_id", "client_secret", "mode"]),
        ["bank-transfer"] = ("Bank Transfer", "Payment Gateways", [], ["account_name", "iban", "bank_name", "instructions"]),
        ["namecheap"] = ("Namecheap", "Domain Registrars", ["api_key", "api_username", "client_ip"], ["api_key", "api_username", "client_ip"]),
        ["resellerclub"] = ("ResellerClub", "Domain Registrars", ["reseller_id", "api_key"], ["reseller_id", "api_key"]),
        ["enom"] = ("ENOM", "Domain Registrars", ["account_id", "api_key"], ["account_id", "api_key"]),
        ["cpanel"] = ("cPanel WHM", "Hosting / Provisioning", ["host", "username", "api_token"], ["host", "port", "username", "api_token"]),
        ["plesk"] = ("Plesk", "Hosting / Provisioning", ["host", "username", "password"], ["host", "port", "username", "password"]),
        ["cwp"] = ("CentOS Web Panel", "Hosting / Provisioning", ["host", "api_key"], ["host", "port", "api_key"]),
        ["smtp"] = ("SMTP Server", "Email / SMTP", ["host", "username", "password", "from_address"], ["host", "port", "username", "password", "from_address", "encryption"]),
        ["maxmind"] = ("MaxMind", "Fraud Protection", ["account_id", "license_key"], ["account_id", "license_key"]),
    };

    /// <summary>
    /// Checks whether an integration is enabled in the settings lookup.
    /// </summary>
    /// <param name="slug">The integration slug.</param>
    /// <param name="lookup">The settings key-value lookup.</param>
    /// <returns>True if enabled setting is "true" (case-insensitive).</returns>
    private static bool IsIntegrationEnabled(string slug, Dictionary<string, string> lookup) =>
        lookup.TryGetValue($"integration:{slug}:is_enabled", out var val)
        && string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns one <see cref="IntegrationListItemDto"/> per known integration.
    /// </summary>
    /// <param name="query">The list integrations query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of 10 integration summary items.</returns>
    public async Task<IReadOnlyList<IntegrationListItemDto>> HandleAsync(
        ListIntegrationsQuery query, CancellationToken ct)
    {
        var all = await settings.ListAsync(ct);

        // Build a lookup: full key -> value for every integration:* setting.
        var lookup = all
            .Where(s => s.Key.StartsWith("integration:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var result = new List<IntegrationListItemDto>(_meta.Count);

        foreach (var (slug, meta) in _meta)
        {
            var isEnabled = IsIntegrationEnabled(slug, lookup);

            var isConfigured = meta.RequiredFields.Length == 0
                || meta.RequiredFields.All(field =>
                    lookup.TryGetValue($"integration:{slug}:{field}", out var val)
                    && !string.IsNullOrWhiteSpace(val));

            result.Add(new IntegrationListItemDto(slug, meta.Name, meta.Category, isEnabled, isConfigured));
        }

        foreach (var plugin in pluginRegistry.GetAll())
        {
            if (_meta.ContainsKey(plugin.Slug))
            {
                continue;
            }

            var isEnabled = IsIntegrationEnabled(plugin.Slug, lookup);

            var isConfigured = plugin.FieldDefinitions
                .Where(f => f.Required)
                .All(f => lookup.TryGetValue($"integration:{plugin.Slug}:{f.Key}", out var val)
                          && !string.IsNullOrWhiteSpace(val));

            result.Add(new IntegrationListItemDto(
                Slug: plugin.Slug,
                Name: plugin.Name,
                Category: plugin.Category,
                IsEnabled: isEnabled,
                IsConfigured: isConfigured,
                IsPlugin: true));
        }

        return result;
    }
}
