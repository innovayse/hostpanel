namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>
/// Refusal codes the fraud area throws as <see cref="DomainRuleException"/>. Declared here because Domain may not reference
/// Application; <c>HostpanelErrorCodes</c> re-declares the same value so every code has a sentence in every language.
/// </summary>
public static class FraudErrors
{
    /// <summary>The value does not fit the block list type it was given for (400).</summary>
    public const string BlockListValueInvalid = "FRAUD_BLOCKLIST_VALUE_INVALID";
}
