using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeRenewalInvoice;

/// <summary>
/// Charges a renewal invoice to the client's saved default card, off session, when the client has agreed to
/// that for the card. Answers with <see cref="RenewalChargeOutcome"/>.
/// </summary>
/// <param name="InvoiceId">The renewal invoice to charge.</param>
public sealed record ChargeRenewalInvoiceCommand(int InvoiceId);
