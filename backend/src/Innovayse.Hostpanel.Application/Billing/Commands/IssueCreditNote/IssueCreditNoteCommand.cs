using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Commands.IssueCreditNote;

/// <summary>
/// Command for staff to issue a credit note against a paid invoice: all of it or part, its tax following the invoice at the same
/// rate, its value given back as account credit or as a gateway refund in one operation.
/// </summary>
/// <param name="InvoiceId">The paid invoice to credit; the route id wins over any value in the body.</param>
/// <param name="Items">The lines credited, before tax; at least one.</param>
/// <param name="Method">Whether the value goes to the client balance or back to the payer through the gateway.</param>
/// <param name="Reason">Why the note is issued; printed on the note, so the client reads it.</param>
public sealed record IssueCreditNoteCommand(
    int InvoiceId, IReadOnlyList<CreditNoteLineInput> Items, CreditNoteMethod Method, string Reason);
