namespace Innovayse.Hostpanel.Application.Clients.Commands.WithdrawClientMarketingConsent;

/// <summary>
/// Command to withdraw a client's consent to marketing email. Sent by the unsubscribe use case, which has already shown
/// that the caller holds the client's personal unsubscribe link; it has no route of its own.
/// </summary>
/// <param name="ClientId">The client that no longer agrees to marketing email.</param>
public sealed record WithdrawClientMarketingConsentCommand(int ClientId);
