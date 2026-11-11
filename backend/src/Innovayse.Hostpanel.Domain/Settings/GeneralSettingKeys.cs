namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The catalogue of settings the general settings endpoints (<c>/api/admin/settings</c>) list, read and
/// write: every portal, billing, plugin-system and campaign key the host seeds, and nothing else.
/// </summary>
/// <remarks>
/// A row outside this set, an integration's switch or field, the setup token or a deployment key
/// such as <c>company.name</c>, has its own path (the integration endpoints) or none at all, so the
/// general endpoints neither return nor accept it. That keeps a secret from ever passing through a
/// plain-text write, and no catalogue key is secret (see <see cref="SecretSettingKeys"/>).
/// </remarks>
public static class GeneralSettingKeys
{
    /// <summary>Every catalogue key, compared ordinally like the settings table's unique key.</summary>
    public static readonly IReadOnlySet<string> All = PortalSettingKeys.Defaults
        .Concat(BillingSettingKeys.Defaults)
        .Concat(PluginSettingKeys.Defaults)
        .Concat(CampaignSettingKeys.Defaults)
        .Select(d => d.Key)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Every seeded default, in portal, billing, plugin, campaign order: the rows a fresh install gets.</summary>
    public static readonly IReadOnlyList<SettingDefault> Defaults = PortalSettingKeys.Defaults
        .Concat(BillingSettingKeys.Defaults)
        .Concat(PluginSettingKeys.Defaults)
        .Concat(CampaignSettingKeys.Defaults)
        .ToList();

    /// <summary>Tells whether a key belongs to the general settings catalogue.</summary>
    /// <param name="key">The full setting key.</param>
    /// <returns><c>true</c> when the general endpoints may list, read and write the key.</returns>
    public static bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return All.Contains(key);
    }

    /// <summary>Finds the shape a catalogue key's free-text value must have, whichever group the key belongs to.</summary>
    /// <param name="key">The full setting key.</param>
    /// <param name="pattern">The key's pattern when it has one.</param>
    /// <returns><c>true</c> when the key has a pattern; a key with a fixed vocabulary or no shape has none.</returns>
    public static bool TryGetPattern(string key, out ValuePattern pattern)
    {
        ArgumentNullException.ThrowIfNull(key);

        return PortalSettingKeys.Patterns.TryGetValue(key, out pattern!)
            || BillingSettingKeys.Patterns.TryGetValue(key, out pattern!)
            || PluginSettingKeys.Patterns.TryGetValue(key, out pattern!)
            || CampaignSettingKeys.Patterns.TryGetValue(key, out pattern!);
    }

    /// <summary>Tells whether a catalogue key may be saved empty although it seeds with a value or has a shape.</summary>
    /// <param name="key">The full setting key.</param>
    /// <returns><c>true</c> when an empty value is a valid "unset" for the key.</returns>
    public static bool AllowsEmpty(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return PortalSettingKeys.AllowsEmpty(key) || BillingSettingKeys.AllowsEmpty(key) || PluginSettingKeys.AllowsEmpty(key);
    }
}
