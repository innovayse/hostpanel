using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillInvoicedOrder;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyInvoiceCredit;

/// <summary>
/// Moves credit from the client's balance onto an invoice, reducing the amount owed. Never more
/// than the balance holds or the invoice still owes. Credit that covers everything the invoice owes
/// settles it in the same save (a part-paid invoice always, an unpaid one when the command asks), and the order it was raised for is
/// fulfilled after that save.
/// </summary>
public sealed class ApplyInvoiceCreditHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Takes the credit off the client's balance in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Stamps the moment a credit that covers the rest settles the invoice.</summary>
    private readonly IClock _clock;

    /// <summary>Wolverine bus, used to fulfil the order of an invoice the credit settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Takes the credit off the client's balance in the same save.</param>
    /// <param name="clock">Stamps the moment a credit that covers the rest settles the invoice.</param>
    /// <param name="bus">Wolverine bus, used to fulfil the order of an invoice the credit settled.</param>
    public ApplyInvoiceCreditHandler(IHostpanelDbContext context, IClientCredit clientCredit, IClock clock, IMessageBus bus)
    {
        _context = context;
        _clientCredit = clientCredit;
        _clock = clock;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ApplyInvoiceCreditCommand"/>.</summary>
    /// <param name="cmd">The apply credit command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> when the id names no invoice or <c>CLIENT_NOT_FOUND</c>
    /// when its client is gone, or <c>CLIENT_CREDIT_INSUFFICIENT</c> when the balance is short; nothing
    /// is saved on a refusal. From the domain: <c>INVOICE_CREDIT_EXCEEDS_DUE</c> when the invoice owes less,
    /// <c>INVOICE_NOT_EDITABLE</c> when it no longer takes credit, and <c>INVOICE_TOP_UP_NO_CREDIT</c> for a credit top-up.
    /// </returns>
    public async Task<Result> HandleAsync(ApplyInvoiceCreditCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        var applied = await _clientCredit.ApplyFromBalanceAsync(invoice, cmd.Amount, CreditMovementType.InvoiceApplied, ct);
        if (applied.IsFailure)
        {
            return Result.FailureCode(applied.ErrorCode!);
        }

        // Credit that covers what a partial payment left owes nothing more: the invoice is settled in this save, so it does not
        // stay open, go overdue and suspend a client who has paid. Credit that covers an invoice nothing was paid on settles it only
        // when the caller asks (a client paying with credit always does): staff may apply credit as one step of editing the invoice,
        // and then the credit itself is the payment.
        var now = _clock.UtcNow;
        if (!invoice.SettleIfCovered(now) && cmd.SettleWhenCovered && invoice.IsPayable && invoice.AmountDue == 0m)
        {
            invoice.MarkPaid(PaymentMethodNames.CreditBalance, now);
        }

        await _context.SaveChangesAsync(ct);

        // After the save, like every other payment: an order is fulfilled only once its invoice is paid on the books, and the
        // fulfilment is idempotent, so a replay finishes what a failed first attempt left.
        if (invoice.Status == InvoiceStatus.Paid)
        {
            await _bus.InvokeAsync(new FulfillInvoicedOrderCommand(invoice.Id), ct);
        }

        return Result.Success();
    }
}
