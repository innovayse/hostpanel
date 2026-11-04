namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The outcome of checking one submitted integration field value: either the canonical value to store, or the
/// resource key (and arguments) of the expectation the value missed. A separate flag tells the two apart, so a
/// value that happens to look like a key is never mistaken for a refusal; the handler words the refusal.
/// </summary>
public sealed record IntegrationFieldValueCheck
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="isValid">Whether the value fits the field.</param>
    /// <param name="text">The canonical value when valid, the expectation's resource key otherwise.</param>
    /// <param name="arguments">The expectation's format arguments; empty when valid.</param>
    private IntegrationFieldValueCheck(bool isValid, string text, object[] arguments)
    {
        IsValid = isValid;
        Text = text;
        Arguments = arguments;
    }

    /// <summary>Gets a value indicating whether the submitted value fits the field.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the canonical value to store when valid, or the expectation's resource key when not.</summary>
    public string Text { get; }

    /// <summary>Gets the format arguments of the expectation's sentence; empty when the value is valid.</summary>
    public object[] Arguments { get; }

    /// <summary>Builds the outcome of a value that fits.</summary>
    /// <param name="canonical">The value to store, in the spelling the field declares.</param>
    /// <returns>The outcome.</returns>
    public static IntegrationFieldValueCheck Accepted(string canonical)
    {
        return new IntegrationFieldValueCheck(true, canonical, []);
    }

    /// <summary>Builds the outcome of a value that does not fit.</summary>
    /// <param name="expectationKey">The resource key of what the field accepts, without the "Expected" frame.</param>
    /// <param name="arguments">The format arguments of that sentence.</param>
    /// <returns>The outcome, carrying the key and arguments.</returns>
    public static IntegrationFieldValueCheck Refused(string expectationKey, params object[] arguments)
    {
        return new IntegrationFieldValueCheck(false, expectationKey, arguments);
    }
}
