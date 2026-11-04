namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The outcome of checking one submitted integration field value: either the canonical value to store, or the
/// sentence naming what the field accepts. A separate flag tells the two apart, so a value that happens to start
/// with the sentence's words is never mistaken for a refusal.
/// </summary>
public sealed record IntegrationFieldValueCheck
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="isValid">Whether the value fits the field.</param>
    /// <param name="text">The canonical value when valid, the "Expected: ..." sentence otherwise.</param>
    private IntegrationFieldValueCheck(bool isValid, string text)
    {
        IsValid = isValid;
        Text = text;
    }

    /// <summary>Gets a value indicating whether the submitted value fits the field.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the canonical value to store when valid, or the "Expected: ..." sentence when not.</summary>
    public string Text { get; }

    /// <summary>Builds the outcome of a value that fits.</summary>
    /// <param name="canonical">The value to store, in the spelling the field declares.</param>
    /// <returns>The outcome.</returns>
    public static IntegrationFieldValueCheck Accepted(string canonical)
    {
        return new IntegrationFieldValueCheck(true, canonical);
    }

    /// <summary>Builds the outcome of a value that does not fit.</summary>
    /// <param name="expectation">What the field accepts, without the "Expected: " prefix.</param>
    /// <returns>The outcome, carrying the complete sentence.</returns>
    public static IntegrationFieldValueCheck Refused(string expectation)
    {
        return new IntegrationFieldValueCheck(false, $"Expected: {expectation}.");
    }
}
