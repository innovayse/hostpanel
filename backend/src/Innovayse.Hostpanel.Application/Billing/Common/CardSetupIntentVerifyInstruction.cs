namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Names the setup intent whose outcome is read back from the card gateway.</summary>
/// <param name="SetupIntentId">The gateway's id of the setup intent.</param>
public sealed record CardSetupIntentVerifyInstruction(string SetupIntentId);
