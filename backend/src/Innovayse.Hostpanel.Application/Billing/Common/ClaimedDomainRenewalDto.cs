namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A paid domain renewal line left claimed after an unknown registrar outcome, waiting for staff.</summary>
/// <param name="InvoiceId">The invoice the line is on.</param>
/// <param name="ItemId">The line.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
/// <param name="RegisteredDomainId">The domain the line renews.</param>
/// <param name="Description">The line text, which names the domain and the term.</param>
/// <param name="PeriodStart">Start of the period the line pays for.</param>
/// <param name="PeriodEnd">End of the period the line pays for.</param>
/// <param name="ClaimedAt">When the renewal was claimed, just before the registrar was asked.</param>
public sealed record ClaimedDomainRenewalDto(
    int InvoiceId,
    int ItemId,
    int ClientId,
    int RegisteredDomainId,
    string Description,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset ClaimedAt);
