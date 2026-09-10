namespace Innovayse.Application.Admin.Common;

/// <summary>
/// Thrown when an operator writes a value that a setting does not accept — a template name
/// the storefront cannot render, a colour mode it does not know, a boolean spelt some third
/// way, or a free-text value with the wrong shape (a colour without its <c>#</c>, a chat
/// address that is not <c>https://</c>).
/// </summary>
/// <remarks>
/// <para>
/// Its own type rather than a bare <see cref="InvalidOperationException"/> because the admin
/// panel has to tell "bad value" apart from "row not found": the first keeps the operator's
/// typed text in the input and shows what would have been accepted, the second reloads the
/// list. Both would otherwise answer <c>INVALID_OPERATION</c>.
/// </para>
/// <para>
/// The sentence is an English literal on purpose. Only the admin SPA writes settings, it is
/// English-only, and <c>rules/project.md</c> records that ordering as deliberate; a refusal a
/// customer reads goes through <c>ValidationMessages*.resx</c> instead.
/// </para>
/// </remarks>
public sealed class InvalidSettingValueException : Exception
{
    /// <summary>Machine-readable code sent as the <c>code</c> field of the error body.</summary>
    public const string Code = "INVALID_SETTING_VALUE";

    /// <summary>The setting key that was written to, for the log line beside the refusal.</summary>
    public string Key { get; }

    /// <summary>
    /// A value outside a fixed vocabulary.
    /// </summary>
    /// <param name="key">The setting key that was written to.</param>
    /// <param name="value">The value that was refused, as the caller sent it.</param>
    /// <param name="allowed">Every value the key does accept.</param>
    public InvalidSettingValueException(string key, string value, IEnumerable<string> allowed)
        : base($"Setting '{key}' does not accept '{value}'. Allowed: {string.Join(", ", allowed)}.")
    {
        Key = key;
    }

    /// <summary>
    /// A value with the wrong shape.
    /// </summary>
    /// <param name="key">The setting key that was written to.</param>
    /// <param name="value">The value that was refused, as the caller sent it.</param>
    /// <param name="expected">What a valid value looks like, e.g. <c>a colour like #1a73e8</c>.</param>
    public InvalidSettingValueException(string key, string value, string expected)
        : base($"Setting '{key}' does not accept '{value}'. Expected: {expected}.")
    {
        Key = key;
    }
}
