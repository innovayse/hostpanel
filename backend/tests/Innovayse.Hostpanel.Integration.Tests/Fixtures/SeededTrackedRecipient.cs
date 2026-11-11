namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A sending campaign with one recipient whose mail has both of its tokens issued, as the sender leaves it just before a mail goes out.</summary>
/// <param name="CampaignId">The campaign.</param>
/// <param name="ClientId">The recipient's client.</param>
/// <param name="Email">The recipient's address.</param>
/// <param name="UnsubscribeToken">The token of the unsubscribe link.</param>
/// <param name="TrackingToken">The token of the open pixel and the tracked links.</param>
public sealed record SeededTrackedRecipient(int CampaignId, int ClientId, string Email, string UnsubscribeToken, string TrackingToken);
