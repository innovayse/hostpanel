namespace Innovayse.Hostpanel.Application.Billing.Commands.ReleaseDomainRenewalClaim;

/// <summary>
/// Staff release a domain renewal line left claimed after an unknown registrar outcome, once they have checked
/// at the registrar that the domain was not renewed, so the next renewal sweep tries it again.
/// </summary>
/// <param name="InvoiceId">The invoice the line is on; set from the route.</param>
/// <param name="ItemId">The line whose claim is released; set from the route.</param>
public sealed record ReleaseDomainRenewalClaimCommand(int InvoiceId, int ItemId);
