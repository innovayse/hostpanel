using System.Globalization;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// One value a plugin keeps in its host-kept store (the SDK's <c>IPluginStore</c>), scoped to the plugin and to the
/// opaque client reference of the call that wrote it. A row with a client reference is per client (every client area
/// call, and an admin call once it carries a verified client); a row without one is plugin wide (an admin call that is
/// not about a client). A key or a value that breaks the SDK's contract is the plugin author's fault, not a caller's
/// refusal: it throws <see cref="DomainRuleException"/> with a <see cref="PluginStoreErrors"/> code, which the call runner
/// answers like any other plugin throw, 502 <c>PLUGIN_FAILED</c>, so the code never reaches a caller.
/// </summary>
public sealed partial class PluginDataEntry : BaseEntity, IAggregateRoot
{
    /// <summary>The longest key accepted, in characters, as the SDK documents it.</summary>
    public const int MaxKeyLength = 200;

    /// <summary>
    /// The longest value accepted, in characters (64 Ki). The SDK leaves the cap to the host: this fits a plugin's
    /// settings, cursor or small document; anything larger belongs in the plugin's own storage.
    /// </summary>
    public const int MaxValueLength = 65536;

    /// <summary>The column length of an opaque client reference: 43 URL-safe Base64 characters today, with room to spare.</summary>
    public const int ClientRefLength = 64;

    /// <summary>The accepted key shape: letters, digits, <c>.</c>, <c>-</c>, <c>_</c> and <c>:</c>, anchored at both ends.</summary>
    private const string KeyPattern = @"\A[A-Za-z0-9.\-_:]+\z";

    /// <summary>The sentence of a refused key; it names the rule, never the key a plugin passed.</summary>
    private static readonly string KeyRule = string.Create(
        CultureInfo.InvariantCulture,
        $"A plugin store key is 1 to {MaxKeyLength} characters of letters, digits, '.', '-', '_' and ':'.");

    /// <summary>The sentence of a refused value; it names the rule, never the value a plugin passed.</summary>
    private static readonly string ValueRule = string.Create(
        CultureInfo.InvariantCulture, $"A plugin store value is text of at most {MaxValueLength} characters.");

    /// <summary>Gets the plugin the value belongs to.</summary>
    public string PluginId { get; private set; } = string.Empty;

    /// <summary>Gets the plugin's opaque reference of the client, or <see langword="null"/> for a plugin-wide value.</summary>
    public string? ClientRef { get; private set; }

    /// <summary>Gets the key the plugin chose.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the text the plugin stored; may be empty.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets when the key was first written in this scope; stamped on insert.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets when the value was last replaced, or <see langword="null"/> while it is the first one; stamped on change.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Whether <paramref name="key"/> is a key the store accepts.</summary>
    /// <param name="key">The key a plugin passed.</param>
    /// <returns><see langword="true"/> when it is 1 to <see cref="MaxKeyLength"/> characters of the accepted set.</returns>
    public static bool IsValidKey(string? key)
    {
        return !string.IsNullOrEmpty(key) && key.Length <= MaxKeyLength && KeyRegex().IsMatch(key);
    }

    /// <summary>Whether <paramref name="value"/> is a value the store accepts.</summary>
    /// <param name="value">The value a plugin passed.</param>
    /// <returns><see langword="true"/> when it is not <see langword="null"/> and at most <see cref="MaxValueLength"/> characters; an empty text is a value.</returns>
    public static bool IsValidValue(string? value)
    {
        return value is not null && value.Length <= MaxValueLength;
    }

    /// <summary>EF Core materialisation only.</summary>
    private PluginDataEntry()
    {
    }

    /// <summary>Starts a value for one key in one scope. The only way a row comes into existence.</summary>
    /// <param name="pluginId">The plugin the value belongs to.</param>
    /// <param name="clientRef">The plugin's opaque reference of the call's client, or <see langword="null"/> for a plugin-wide value.</param>
    /// <param name="key">The key, as <see cref="IsValidKey"/> accepts it.</param>
    /// <param name="value">The text to keep, as <see cref="IsValidValue"/> accepts it.</param>
    /// <returns>The new, unsaved value.</returns>
    /// <exception cref="DomainRuleException">Thrown when the key or the value breaks the SDK's contract.</exception>
    public static PluginDataEntry Create(string pluginId, string? clientRef, string key, string value)
    {
        EnsureKey(key);
        EnsureValue(value);
        return new PluginDataEntry { PluginId = pluginId, ClientRef = clientRef, Key = key, Value = value };
    }

    /// <summary>Refuses a key the SDK does not allow. Every store operation asks this before it reads or writes.</summary>
    /// <param name="key">The key a plugin passed.</param>
    /// <exception cref="DomainRuleException">Thrown when the key breaks the SDK's key rule.</exception>
    public static void EnsureKey(string? key)
    {
        if (!IsValidKey(key))
        {
            throw new DomainRuleException(PluginStoreErrors.KeyInvalid, KeyRule);
        }
    }

    /// <summary>Replaces the stored text. The moment of the change is stamped on save.</summary>
    /// <param name="value">The new text, as <see cref="IsValidValue"/> accepts it.</param>
    /// <exception cref="DomainRuleException">Thrown when the value breaks the SDK's contract.</exception>
    public void ChangeValue(string value)
    {
        EnsureValue(value);
        Value = value;
    }

    /// <summary>Refuses a value the store does not keep.</summary>
    /// <param name="value">The value a plugin passed.</param>
    /// <exception cref="DomainRuleException">Thrown when the value is missing or over <see cref="MaxValueLength"/> characters.</exception>
    private static void EnsureValue(string? value)
    {
        if (!IsValidValue(value))
        {
            throw new DomainRuleException(PluginStoreErrors.ValueInvalid, ValueRule);
        }
    }

    /// <summary>The compiled <see cref="KeyPattern"/>.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(KeyPattern, RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}
