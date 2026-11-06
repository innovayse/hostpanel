using Innovayse.Hostpanel.Application.Billing.Commands.ApplyInvoiceCredit;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyMyInvoiceCredit;

/// <summary>
/// Applies credit from the calling client's own balance to one of their own invoices, refusing every invoice that is not theirs.
/// </summary>
/// <remarks>
/// The ownership check lives here rather than at the endpoint, so it travels with the message. Once ownership is settled the
/// work is the one staff run, so this dispatches <see cref="ApplyInvoiceCreditCommand"/>: the balance check, the invoice rules
/// (a credit top-up takes none, no more than is owed) and the single save stay in one place.
/// </remarks>
public sealed class ApplyMyInvoiceCreditHandler
{
    /// <summary>The rule that says a client may only act on their own invoices.</summary>
    private readonly IInvoiceOwnership _ownership;

    /// <summary>The module's data context, for the invoice's account.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, to ask whether the account may transact and to reach the shared command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only act on their own invoices.</param>
    /// <param name="context">The module's data context, for the invoice's account.</param>
    /// <param name="bus">Wolverine bus, to ask whether the account may transact and to reach the shared command.</param>
    public ApplyMyInvoiceCreditHandler(IInvoiceOwnership ownership, IHostpanelDbContext context, IMessageBus bus)
    {
        _ownership = ownership;
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="ApplyMyInvoiceCreditCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this applies the caller's own credit to the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c> (404) when the invoice is not the caller's; <c>CLIENT_SUSPENDED</c> or
    /// <c>CLIENT_CLOSED</c> when the account may not transact; and the refusals of <see cref="ApplyInvoiceCreditCommand"/>:
    /// <c>CLIENT_CREDIT_INSUFFICIENT</c>, <c>INVOICE_CREDIT_EXCEEDS_DUE</c>, <c>INVOICE_NOT_EDITABLE</c> and
    /// <c>INVOICE_TOP_UP_NO_CREDIT</c>.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(ApplyMyInvoiceCreditCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        var clientId = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Id == cmd.InvoiceId)
            .Select(i => (int?)i.ClientId)
            .FirstOrDefaultAsync(ct);
        if (clientId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        var mayPay = await _bus.EnsureClientMayTransactAsync(clientId.Value, ct);
        if (mayPay.IsFailure)
        {
            return mayPay;
        }

        return await _bus.InvokeAsync<Result>(new ApplyInvoiceCreditCommand(cmd.InvoiceId, cmd.Amount, SettleWhenCovered: true), ct);
    }
}
