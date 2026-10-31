using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The answer to a request for the right to charge an invoice's card.</summary>
/// <param name="Status">What the request came to.</param>
/// <param name="Claim">The held claim, set only when <paramref name="Status"/> is <see cref="InvoicePaymentClaimAcquisition.Acquired"/>.</param>
public sealed record InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition Status, InvoicePaymentClaim? Claim);
