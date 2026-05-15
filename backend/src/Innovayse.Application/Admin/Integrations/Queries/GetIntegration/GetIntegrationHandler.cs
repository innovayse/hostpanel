namespace Innovayse.Application.Admin.Integrations.Queries.GetIntegration;

using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="GetIntegrationQuery"/> by loading all settings for the given slug
/// and returning them with secret values masked alongside field definitions.
/// </summary>
/// <param name="settings">Setting repository for key-value lookups.</param>
/// <param name="plugins">Plugin registry for resolving plugin-provided integrations.</param>
public sealed class GetIntegrationHandler(ISettingRepository settings, IPluginRegistry plugins)
{
    /// <summary>
    /// Substring patterns that identify a field as a secret.
    /// Any field key containing one of these strings (case-insensitive) will be masked.
    /// </summary>
    private static readonly string[] _secretMarkers = ["key", "secret", "password", "token"];

    /// <summary>
    /// Static metadata for every built-in integration.
    /// Tuple: (display name, category label, ordered field definitions).
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Category, IReadOnlyList<FieldDefinitionDto> Fields)> _meta = new()
    {
        ["stripe"] = ("Stripe", "Payment Gateways",
        [
            new("secret_key",      "Secret Key",      "password", Required: true),
            new("publishable_key", "Publishable Key", "text",     Required: true),
            new("webhook_secret",  "Webhook Secret",  "password", Required: false),
            new("mode",            "Mode",            "select",   Required: false, Options: ["Live", "Test"]),
        ]),
        ["paypal"] = ("PayPal", "Payment Gateways",
        [
            new("client_id",     "Client ID",     "text",     Required: true),
            new("client_secret", "Client Secret", "password", Required: true),
            new("mode",          "Mode",          "select",   Required: false, Options: ["Live", "Sandbox"]),
        ]),
        ["bank-transfer"] = ("Bank Transfer", "Payment Gateways",
        [
            new("account_name", "Account Name",         "text",     Required: false),
            new("iban",         "IBAN",                 "text",     Required: false),
            new("bank_name",    "Bank Name",            "text",     Required: false),
            new("instructions", "Payment Instructions", "textarea", Required: false),
        ]),
        ["namecheap"] = ("Namecheap", "Domain Registrars",
        [
            new("api_key",      "API Key",              "password", Required: true),
            new("api_username", "API Username",          "text",     Required: true),
            new("client_ip",    "Whitelisted Client IP", "text",     Required: true),
        ]),
        ["resellerclub"] = ("ResellerClub", "Domain Registrars",
        [
            new("reseller_id", "Reseller ID", "text",     Required: true),
            new("api_key",     "API Key",     "password", Required: true),
        ]),
        ["enom"] = ("ENOM", "Domain Registrars",
        [
            new("account_id", "Account ID", "text",     Required: true),
            new("api_key",    "API Key",    "password", Required: true),
        ]),
        ["cpanel"] = ("cPanel WHM", "Hosting / Provisioning",
        [
            new("host",      "WHM Host",  "text",     Required: true),
            new("port",      "Port",      "text",     Required: false),
            new("username",  "Username",  "text",     Required: true),
            new("api_token", "API Token", "password", Required: true),
        ]),
        ["plesk"] = ("Plesk", "Hosting / Provisioning",
        [
            new("host",     "Plesk Host", "text",     Required: true),
            new("port",     "Port",       "text",     Required: false),
            new("username", "Username",   "text",     Required: true),
            new("password", "Password",   "password", Required: true),
        ]),
        ["cwp"] = ("CentOS Web Panel", "Hosting / Provisioning",
        [
            new("host",    "CWP Host", "text",     Required: true),
            new("port",    "Port",     "text",     Required: false),
            new("api_key", "API Key",  "password", Required: true),
        ]),
        ["smtp"] = ("SMTP Server", "Email / SMTP",
        [
            new("host",         "SMTP Host",          "text",     Required: true),
            new("port",         "Port",               "text",     Required: false),
            new("username",     "Username",           "text",     Required: false),
            new("password",     "Password",           "password", Required: false),
            new("from_address", "From Address",       "text",     Required: true),
            new("encryption",   "Encryption",         "select",   Required: false, Options: ["TLS", "SSL", "None"]),
        ]),
        ["maxmind"] = ("MaxMind", "Fraud Protection",
        [
            new("account_id",  "Account ID",  "text",     Required: true),
            new("license_key", "License Key", "password", Required: true),
        ]),
    };

    /// <summary>
    /// Loads the configuration for the requested integration or plugin, masks secret fields,
    /// and returns field definitions for dynamic form rendering.
    /// </summary>
    /// <param name="query">Query containing the integration slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The integration detail DTO with masked config and field definitions,
    /// or null if the slug is not recognised as a built-in integration or loaded plugin.
    /// </returns>
    public async Task<IntegrationDetailDto?> HandleAsync(GetIntegrationQuery query, CancellationToken ct)
    {
        IReadOnlyList<FieldDefinitionDto> fields;

        if (_meta.TryGetValue(query.Slug, out var meta))
        {
            fields = meta.Fields;
        }
        else
        {
            var manifest = plugins.GetLoadedManifests()
                .FirstOrDefault(m => m.Id == query.Slug);

            if (manifest is null)
            {
                return null;
            }

            fields = manifest.Fields
                .Select(f => new FieldDefinitionDto(f.Key, f.Label, f.Type, f.Required))
                .ToList();
        }

        var all = await settings.ListAsync(ct);
        var prefix = $"integration:{query.Slug}:";

        var lookup = all
            .Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var isEnabled = lookup.TryGetValue($"{prefix}is_enabled", out var enabledVal)
            && string.Equals(enabledVal, "true", StringComparison.OrdinalIgnoreCase);

        var config = new Dictionary<string, string>(fields.Count);
        foreach (var field in fields)
        {
            lookup.TryGetValue($"{prefix}{field.Key}", out var raw);
            config[field.Key] = MaskIfSecret(field.Key, raw ?? string.Empty);
        }

        return new IntegrationDetailDto(query.Slug, isEnabled, config, fields);
    }

    /// <summary>
    /// Returns "••••••••" when <paramref name="fieldKey"/> is a secret field and
    /// <paramref name="value"/> is non-empty; otherwise returns <paramref name="value"/> unchanged.
    /// </summary>
    /// <param name="fieldKey">The field key to inspect.</param>
    /// <param name="value">The raw stored value.</param>
    /// <returns>The original value or the masked placeholder.</returns>
    private static string MaskIfSecret(string fieldKey, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var isSecret = _secretMarkers.Any(m => fieldKey.Contains(m, StringComparison.OrdinalIgnoreCase));
        return isSecret ? "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022" : value;
    }
}
