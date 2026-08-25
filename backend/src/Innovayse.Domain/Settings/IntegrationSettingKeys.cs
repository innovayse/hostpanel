namespace Innovayse.Domain.Settings;

/// <summary>
/// Builds the <c>Settings</c> table keys used to store per-integration configuration
/// (<c>integration:{slug}:{field}</c>). Every layer that reads or writes integration
/// settings — the API, Application handlers, and Infrastructure's plugin resolver —
/// should compose keys through this class instead of re-building the string by hand.
/// </summary>
public static class IntegrationSettingKeys
{
    /// <summary>The field name that stores whether an integration is enabled.</summary>
    public const string IsEnabledField = "is_enabled";

    /// <summary>Builds the key prefix shared by every setting belonging to an integration.</summary>
    /// <param name="slug">The integration's plugin id / slug.</param>
    /// <returns>The prefix, e.g. <c>integration:innovayse-inecobank:</c>.</returns>
    public static string Prefix(string slug) => $"integration:{slug}:";

    /// <summary>Builds the full key for a single configuration field of an integration.</summary>
    /// <param name="slug">The integration's plugin id / slug.</param>
    /// <param name="field">The field name, as declared in the plugin's manifest.</param>
    /// <returns>The full setting key, e.g. <c>integration:innovayse-inecobank:username</c>.</returns>
    public static string FieldKey(string slug, string field) => $"{Prefix(slug)}{field}";

    /// <summary>Builds the key for the enabled/disabled flag of an integration.</summary>
    /// <param name="slug">The integration's plugin id / slug.</param>
    /// <returns>The full setting key, e.g. <c>integration:innovayse-inecobank:is_enabled</c>.</returns>
    public static string EnabledKey(string slug) => FieldKey(slug, IsEnabledField);
}
