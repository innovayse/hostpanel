using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The one registry of which <c>Settings</c> rows hold a secret: a payment gateway key, a registrar
/// or panel password, an SMTP password, a plugin credential or the first-run setup token.
/// </summary>
/// <remarks>
/// <para>
/// A secret setting is write only. No read endpoint returns its value; it answers
/// <c>isSecret</c> and <c>hasValue</c> instead, and an update with an empty value keeps what is
/// stored. Every read and write path asks this class (through
/// <c>Application.Admin.Common.SettingSecrecy</c>, which adds the fields a loaded plugin declares
/// as secret), so a new secret is classified here once and never by matching a substring at the
/// call site.
/// </para>
/// <para>
/// The integration field names are explicit rather than a pattern on purpose. The storefront's
/// chat <c>website_token</c> is public by design and served anonymously, and a field such as
/// <c>access_hash</c> is a credential that no "key/secret/password/token" substring rule catches.
/// </para>
/// </remarks>
public static class SecretSettingKeys
{
    /// <summary>Key the outstanding first-run setup token is stored under.</summary>
    public const string SetupToken = "auth:setup-token";

    /// <summary>
    /// Integration field names (the last segment of <c>integration:{slug}:{field}</c>) that hold a
    /// credential, whichever integration they belong to.
    /// </summary>
    public static readonly IReadOnlySet<string> IntegrationSecretFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "secret_key",
        "webhook_secret",
        "client_secret",
        IntegrationSettingKeys.ApiKeyField,
        IntegrationSettingKeys.PasswordField,
        "api_token",
        "license_key",
        IntegrationSettingKeys.AccessHashField,
    };

    /// <summary>Whole setting keys outside the integration namespace that hold a secret.</summary>
    public static readonly IReadOnlySet<string> StandaloneKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SetupToken,
    };

    /// <summary>Tells whether a field input kind marks the field as a secret.</summary>
    /// <param name="fieldType">The input kind from a plugin manifest or the integration catalogue.</param>
    /// <returns><c>true</c> for <see cref="IntegrationFieldType.Secret"/> and <see cref="IntegrationFieldType.Password"/>.</returns>
    public static bool IsSecretFieldType(IntegrationFieldType fieldType)
    {
        return fieldType is IntegrationFieldType.Secret or IntegrationFieldType.Password;
    }

    /// <summary>Tells whether a full setting key names a secret this registry knows.</summary>
    /// <param name="key">The full setting key, e.g. <c>integration:gateway:secret_key</c>.</param>
    /// <returns><c>true</c> when the key is a standalone secret or an integration secret field.</returns>
    public static bool IsSecret(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (StandaloneKeys.Contains(key) || key.StartsWith(PluginSettingKeys.RegistryLicencePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!key.StartsWith(IntegrationSettingKeys.RootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // integration:{slug}:{field}; the field is the segment after the last colon.
        var separator = key.LastIndexOf(':');
        var field = key[(separator + 1)..];
        return separator >= IntegrationSettingKeys.RootPrefix.Length && IntegrationSecretFields.Contains(field);
    }
}
