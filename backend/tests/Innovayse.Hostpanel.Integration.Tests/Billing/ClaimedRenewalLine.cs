namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>The ids of a seeded domain renewal line.</summary>
/// <param name="ClientId">The client.</param>
/// <param name="InvoiceId">The invoice.</param>
/// <param name="ItemId">The renewal line.</param>
public sealed record ClaimedRenewalLine(int ClientId, int InvoiceId, int ItemId);
