using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The operator settings of the plugin system. Kept apart from <see cref="PortalSettingKeys"/> on purpose:
/// that list feeds the anonymous storefront endpoint, and none of these is public. Seeded by
/// <c>PortalSettingsSeeder</c>, edited on the admin plugins page, validated by <c>UpdateSettingHandler</c>.
/// </summary>
public static class PluginSettingKeys
{
    /// <summary>
    /// Whether a host running as Production still installs and loads an uploaded plugin whose signature is
    /// missing, tampered or made with an unknown key. Off by default. Read at startup (to decide which plugins
    /// load) and on every install.
    /// </summary>
    public const string AllowUnsigned = "plugins.allow_unsigned";

    /// <summary>The stored value of a switch that is on.</summary>
    public const string On = "true";

    /// <summary>The stored value of a switch that is off.</summary>
    public const string Off = "false";

    /// <summary>The shape each key accepts, with its canonical spelling.</summary>
    public static readonly IReadOnlyDictionary<string, ValuePattern> Patterns =
        new Dictionary<string, ValuePattern>
        {
            [AllowUnsigned] = new(
                new Regex("^(true|false)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                "true or false",
                v => v.ToLowerInvariant()),
        };

    /// <summary>Every key with its seeded value and the description shown in System Settings.</summary>
    public static readonly IReadOnlyList<SettingDefault> Defaults =
    [
        new(AllowUnsigned, Off, "Install and load uploaded plugins whose signature is missing or invalid, in a production host (true/false). Off by default."),
    ];

    /// <summary>Keys whose every change is written to the activity log: they weaken a security control.</summary>
    private static readonly IReadOnlySet<string> Audited = new HashSet<string>(StringComparer.Ordinal) { AllowUnsigned };

    /// <summary>Whether a change of <paramref name="key"/> is written to the activity log.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns><see langword="true"/> for a plugin key that weakens a security control.</returns>
    public static bool IsAudited(string key) => Audited.Contains(key);

    /// <summary>Whether a stored value reads as an enabled switch.</summary>
    /// <param name="value">The stored value, or <see langword="null"/> when the row does not exist.</param>
    /// <returns><see langword="true"/> only for <c>true</c>, in any case; a missing or odd value is off.</returns>
    public static bool IsOn(string? value) => string.Equals(value, On, StringComparison.OrdinalIgnoreCase);
}
