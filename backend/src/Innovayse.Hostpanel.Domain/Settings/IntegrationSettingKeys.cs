namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// Builds the <c>Settings</c> table keys used to store per-integration configuration
/// (<c>integration:{slug}:{field}</c>). Every layer that reads or writes integration
/// settings, the API, Application handlers, and Infrastructure's plugin resolver,
/// should compose keys through this class instead of re-building the string by hand.
/// </summary>
public static class IntegrationSettingKeys
{
    /// <summary>The key prefix every integration setting starts with.</summary>
    public const string RootPrefix = "integration:";

    /// <summary>The field name that stores whether an integration is enabled.</summary>
    public const string IsEnabledField = "is_enabled";

    /// <summary>The stored value of an enabled flag that is on. Compared case-insensitively.</summary>
    public const string EnabledValue = "true";

    /// <summary>The stored value of an enabled flag that is off.</summary>
    public const string DisabledValue = "false";

    /// <summary>The field name that stores a registrar account's password.</summary>
    public const string PasswordField = "password";

    /// <summary>The field name that stores a hosting panel's API key.</summary>
    public const string ApiKeyField = "api_key";

    /// <summary>The field name that stores a hosting panel's access hash.</summary>
    public const string AccessHashField = "access_hash";

    /// <summary>Builds the configuration section path that holds every setting of an integration.</summary>
    /// <param name="slug">The integration's plugin id / slug.</param>
    /// <returns>The section path, e.g. <c>integration:{slug}</c>.</returns>
    public static string Section(string slug) => $"{RootPrefix}{slug}";

    /// <summary>Builds the key prefix shared by every setting belonging to an integration.</summary>
    /// <param name="slug">The integration's plugin id / slug.</param>
    /// <returns>The prefix, e.g. <c>integration:{slug}:</c>.</returns>
    public static string Prefix(string slug) => $"{Section(slug)}:";

    /// <summary>Builds the full key for a single configuration field of an integration.</summary>
    /// <param name="slug">The integration's plugin id / slug.</param>
    /// <param name="field">The field name, as declared in the plugin's manifest.</param>
    /// <returns>The full setting key, e.g. <c>integration:{slug}:username</c>.</returns>
    public static string FieldKey(string slug, string field) => $"{Prefix(slug)}{field}";

    /// <summary>Builds the key for the enabled/disabled flag of an integration.</summary>
    /// <param name="slug">The integration's plugin id / slug.</param>
    /// <returns>The full setting key, e.g. <c>integration:{slug}:is_enabled</c>.</returns>
    public static string EnabledKey(string slug) => FieldKey(slug, IsEnabledField);
}
