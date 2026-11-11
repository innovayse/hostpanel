namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A campaign that is sending, with the one recipient the tests act on.</summary>
/// <param name="CampaignId">The sending campaign's primary key.</param>
/// <param name="ClientId">The recipient client's primary key.</param>
/// <param name="Email">The recipient client's sign-in address, which the mail goes to.</param>
/// <param name="Token">The recipient's unsubscribe token.</param>
public sealed record SeededCampaignRecipient(int CampaignId, int ClientId, string Email, string Token);
