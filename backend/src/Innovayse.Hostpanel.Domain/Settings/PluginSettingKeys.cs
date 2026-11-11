using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Domain.Plugins.Enums;

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

    /// <summary>
    /// The widgets the operator hides from a slot: a comma separated list of <c>slot:plugin:widget</c> entries, built
    /// with <see cref="HiddenWidgetEntry"/>. Empty by default, which hides nothing. A hidden widget is left out of the
    /// slot index; the plugin stays enabled and its other widgets are unaffected.
    /// </summary>
    public const string HiddenSlotWidgets = "plugins.hidden_slot_widgets";

    /// <summary>
    /// Whose plugins the plugin registry may install: the name of a <see cref="PluginRegistryTrust"/>. Seeded
    /// <see cref="PluginRegistryTrust.VerifiedOnly"/> by the server. Read on every registry install.
    /// </summary>
    public const string RegistryTrust = "plugins.registry_trust";

    /// <summary>
    /// The prefix of the per-plugin licence key a paid or private registry plugin asks for. The full key is
    /// <see cref="RegistryLicenceKey"/>. The value is a secret (<c>SecretSettingKeys</c>): stored encrypted, never returned.
    /// </summary>
    public const string RegistryLicencePrefix = "plugins.registry_licence:";

    /// <summary>
    /// The identity this installation uses towards the plugin registry: its Ed25519 key pair, the instance id and the
    /// challenge the registry gave it, and where the registration stands, kept as one JSON document. The value is a secret
    /// (<c>SecretSettingKeys</c>): it holds the private key, so the whole document is stored encrypted and never returned.
    /// The row is outside the general settings catalogue, so neither the settings list nor a settings update reaches it.
    /// </summary>
    public const string RegistryInstance = "plugins.registry_instance";

    /// <summary>The stored value of a switch that is on.</summary>
    public const string On = "true";

    /// <summary>The stored value of a switch that is off.</summary>
    public const string Off = "false";

    /// <summary>One <c>slot:plugin:widget</c> entry: three non-empty parts with no comma, colon or whitespace in them.</summary>
    private const string HiddenEntryPattern = @"[^,:\s]+:[^,:\s]+:[^,:\s]+";

    /// <summary>
    /// The whole stored list: entries separated by commas, where an empty entry (<c>a:b:c,,</c>) is allowed and ignored.
    /// <see cref="ParseHidden"/> reads the same grammar, so a value this accepts reads back and a value it refuses never does.
    /// </summary>
    private const string HiddenListPattern =
        @"\A(?:\s*,)*\s*(?:" + HiddenEntryPattern + @"(?:\s*,(?:\s*,)*\s*" + HiddenEntryPattern + @")*(?:\s*,)*\s*)?\z";

    /// <summary>One entry on its own, for <see cref="ParseHidden"/>.</summary>
    private static readonly Regex HiddenEntry = new(@"\A" + HiddenEntryPattern + @"\z", RegexOptions.CultureInvariant);

    /// <summary>The shape each key accepts, with its canonical spelling.</summary>
    public static readonly IReadOnlyDictionary<string, ValuePattern> Patterns =
        new Dictionary<string, ValuePattern>
        {
            [AllowUnsigned] = new(
                new Regex("^(true|false)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                "true or false",
                v => v.ToLowerInvariant()),
            [RegistryTrust] = new(
                new Regex(
                    "^(" + string.Join("|", Enum.GetNames<PluginRegistryTrust>()) + ")$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                string.Join(", ", Enum.GetNames<PluginRegistryTrust>()),
                v => Enum.GetNames<PluginRegistryTrust>().First(n => string.Equals(n, v, StringComparison.OrdinalIgnoreCase))),
            [HiddenSlotWidgets] = new(
                new Regex(HiddenListPattern, RegexOptions.CultureInvariant),
                "slot:plugin:widget entries separated by commas, such as client.dashboard.cards:acme:usage",
                v => string.Join(",", ParseHidden(v))),
        };

    /// <summary>Every key with its seeded value and the description shown in System Settings.</summary>
    public static readonly IReadOnlyList<SettingDefault> Defaults =
    [
        new(AllowUnsigned, Off, "Install and load uploaded plugins whose signature is missing or invalid, in a production host (true/false). Off by default."),
        new(RegistryTrust, nameof(PluginRegistryTrust.VerifiedOnly), "Whose plugins the plugin registry may install: VerifiedOnly, AnyRegistryPublisher or OwnKeysOnly. VerifiedOnly by default."),
        new(HiddenSlotWidgets, string.Empty, "Plugin widgets hidden from a page slot, as slot:plugin:widget entries separated by commas. Empty hides nothing."),
    ];

    /// <summary>Keys whose every change is written to the activity log: they weaken a security control.</summary>
    private static readonly IReadOnlySet<string> Audited = new HashSet<string>(StringComparer.Ordinal) { AllowUnsigned, RegistryTrust };

    /// <summary>Whether a change of <paramref name="key"/> is written to the activity log.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns><see langword="true"/> for a plugin key that weakens a security control.</returns>
    public static bool IsAudited(string key) => Audited.Contains(key);

    /// <summary>The setting key a plugin's registry licence key is stored under.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The key, <see cref="RegistryLicencePrefix"/> followed by the id.</returns>
    public static string RegistryLicenceKey(string pluginId)
    {
        return RegistryLicencePrefix + pluginId;
    }

    /// <summary>Whether <paramref name="key"/> is a plugin key that may be saved empty.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns><see langword="true"/> for <see cref="HiddenSlotWidgets"/>, whose empty value hides nothing.</returns>
    public static bool AllowsEmpty(string key) => string.Equals(key, HiddenSlotWidgets, StringComparison.Ordinal);

    /// <summary>Writes one entry of <see cref="HiddenSlotWidgets"/>.</summary>
    /// <param name="slotId">The slot the widget is placed in.</param>
    /// <param name="pluginId">The plugin that declares the widget.</param>
    /// <param name="widgetId">The widget's id within the plugin.</param>
    /// <returns>The entry, which is how <see cref="HiddenWidgets"/> reads it back.</returns>
    public static string HiddenWidgetEntry(string slotId, string pluginId, string widgetId) => $"{slotId}:{pluginId}:{widgetId}";

    /// <summary>Reads the stored value of <see cref="HiddenSlotWidgets"/>.</summary>
    /// <param name="value">The stored value, or <see langword="null"/> when the row does not exist.</param>
    /// <returns>The hidden entries, compared exactly; empty for a missing or empty value; empty and malformed entries are ignored.</returns>
    public static IReadOnlySet<string> HiddenWidgets(string? value)
    {
        return ParseHidden(value).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The one parse of <see cref="HiddenSlotWidgets"/>: split on commas, trim, drop empty entries and malformed ones.</summary>
    /// <param name="value">The stored or submitted value, or <see langword="null"/>.</param>
    /// <returns>The well formed entries in order; the validator refuses a malformed entry before it is ever stored.</returns>
    private static IEnumerable<string> ParseHidden(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => HiddenEntry.IsMatch(entry));
    }

    /// <summary>Reads the stored value of <see cref="RegistryTrust"/>.</summary>
    /// <param name="value">The stored value, or <see langword="null"/> when the row does not exist.</param>
    /// <returns>
    /// The policy the name stands for (any case); a missing, empty or unknown value reads as
    /// <see cref="PluginRegistryTrust.VerifiedOnly"/>, the strictest choice that still installs from the registry, so a damaged
    /// row never widens trust.
    /// </returns>
    public static PluginRegistryTrust ParseRegistryTrust(string? value)
    {
        return Enum.TryParse<PluginRegistryTrust>(value, ignoreCase: true, out var trust) && Enum.IsDefined(trust)
            ? trust
            : PluginRegistryTrust.VerifiedOnly;
    }

    /// <summary>Whether a stored value reads as an enabled switch.</summary>
    /// <param name="value">The stored value, or <see langword="null"/> when the row does not exist.</param>
    /// <returns><see langword="true"/> only for <c>true</c>, in any case; a missing or odd value is off.</returns>
    public static bool IsOn(string? value) => string.Equals(value, On, StringComparison.OrdinalIgnoreCase);
}
