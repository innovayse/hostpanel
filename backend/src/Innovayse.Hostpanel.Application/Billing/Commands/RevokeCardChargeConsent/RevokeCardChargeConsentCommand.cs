namespace Innovayse.Hostpanel.Application.Billing.Commands.RevokeCardChargeConsent;

/// <summary>
/// Revokes one of the authenticated client's stored charge consents. The host never charges the method again, and the
/// method is removed at the gateway on a best effort basis.
/// </summary>
/// <remarks>Carries no client id: the consent is found for the caller's own account only.</remarks>
/// <param name="ConsentId">The consent to revoke.</param>
public sealed record RevokeCardChargeConsentCommand(int ConsentId);
