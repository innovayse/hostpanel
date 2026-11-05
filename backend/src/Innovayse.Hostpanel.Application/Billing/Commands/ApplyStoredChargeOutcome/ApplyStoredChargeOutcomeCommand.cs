using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyStoredChargeOutcome;

/// <summary>
/// Applies what a payment plugin reported about a stored method charge to its claim and, when the money was taken, to
/// the invoice. The one place a charge's outcome is acted on: the charge itself and the reconciliation pass both end
/// here, so a charge that went through is recorded the same way whichever path found it. Safe to send again for the
/// same claim.
/// </summary>
/// <param name="ClaimId">The claim the charge was made under.</param>
/// <param name="Outcome">
/// How the charge ended, as the plugin reported it; <see langword="null"/> when the plugin's key lookup confirmed it
/// never charged under the claim's key, which ends the claim as abandoned.
/// </param>
/// <param name="TransactionId">The gateway's transaction reference; set when the charge succeeded.</param>
/// <param name="AmountMinor">The amount the gateway says it took, in minor units; set when the charge succeeded.</param>
/// <param name="Currency">ISO 4217 numeric code of the currency the gateway says it charged in; set when the charge succeeded.</param>
/// <param name="ActionUrl">Where to send the payer to authenticate; set when the outcome is <see cref="StoredChargeOutcome.ActionRequired"/>.</param>
public sealed record ApplyStoredChargeOutcomeCommand(
    int ClaimId,
    StoredChargeOutcome? Outcome,
    string? TransactionId,
    long? AmountMinor,
    string? Currency,
    string? ActionUrl);
