using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DeleteTransaction;

/// <summary>
/// Deletes a <see cref="Domain.Billing.Transaction"/> and reverses any credit adjustments.
/// </summary>
public sealed class DeleteTransactionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reverses the transaction's credit adjustment in the same save as the delete.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Reverses the transaction's credit adjustment in the same save as the delete.</param>
    public DeleteTransactionHandler(IHostpanelDbContext context, IClientCredit clientCredit)
    {
        _context = context;
        _clientCredit = clientCredit;
    }

    /// <summary>Handles <see cref="DeleteTransactionCommand"/>.</summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>TRANSACTION_NOT_FOUND</c> when the id names no transaction, or
    /// <c>CLIENT_NOT_FOUND</c> when the transaction's credit adjustment cannot be reversed because
    /// the owning client no longer exists, or <c>CLIENT_CREDIT_INSUFFICIENT</c> when the credit it
    /// added has already been spent; <c>TRANSACTION_CREDIT_LOCKED</c> (from the domain) when the entry is part of the credit history.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteTransactionCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct);
        if (transaction is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.TRANSACTION_NOT_FOUND);
        }

        // An entry of the credit history is a record and stays; a legacy one is deleted and its credit move undone.
        transaction.EnsureEditable();

        if (transaction.AddedToCredit && transaction.AmountIn > 0)
        {
            var taken = await _clientCredit.DeductAsync(
                new ClientCreditChange(transaction.ClientId, transaction.AmountIn, CreditMovementType.TransactionReversed, transaction.InvoiceId), ct);
            if (taken.IsFailure)
            {
                return Result.FailureCode(taken.ErrorCode!);
            }
        }

        if (transaction.AddedToCredit && transaction.AmountOut > 0)
        {
            var added = await _clientCredit.AddAsync(
                new ClientCreditChange(transaction.ClientId, transaction.AmountOut, CreditMovementType.TransactionReversed, transaction.InvoiceId), ct);
            if (added.IsFailure)
            {
                return Result.FailureCode(added.ErrorCode!);
            }
        }

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
