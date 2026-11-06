using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyBankTransfer;

/// <summary>
/// Returns the bank details and the reference of the manual bank transfer waiting on one of the calling client's own
/// invoices, refusing every invoice that is not theirs. The details are shown only while a transfer waits, so the page can
/// be reloaded after choosing it; an invoice that was never switched to a transfer answers that nothing is waiting.
/// </summary>
public sealed class GetMyBankTransferHandler
{
    /// <summary>The rule that says a client may only read their own invoices.</summary>
    private readonly IInvoiceOwnership _ownership;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, for the bank details.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only read their own invoices.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, for the bank details.</param>
    public GetMyBankTransferHandler(IInvoiceOwnership ownership, IHostpanelDbContext context, IMessageBus bus)
    {
        _ownership = ownership;
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetMyBankTransferQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The details, or <c>INVOICE_NOT_FOUND</c> (404) when the invoice is not the caller's, or
    /// <c>INVOICE_BANK_TRANSFER_NOT_PENDING</c> (409) when no transfer waits on it.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<BankTransferDetailsDto>> HandleAsync(GetMyBankTransferQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.InvoiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<BankTransferDetailsDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<BankTransferDetailsDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (!invoice.IsBankTransferPending)
        {
            return Result<BankTransferDetailsDto>.FailureCode(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
        }

        var settings = await _bus.LoadBankTransferSettingsAsync(ct);
        return Result.Success(settings.ToDetails(invoice));
    }
}
