using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Commands.AdjustClientCredit;

/// <summary>
/// Adds credit to a client's balance or takes it off by hand. The adjustment moves no cash, so it is written to the ledger
/// as a credit-only line with the reason staff gave, in the same save as the balance; the client is told the new balance.
/// </summary>
public sealed class AdjustClientCreditHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Moves the balance and writes the history line in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Moves the balance and writes the history line in the same save.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public AdjustClientCreditHandler(IHostpanelDbContext context, IClientCredit clientCredit, IActivityLogger activity)
    {
        _context = context;
        _clientCredit = clientCredit;
        _activity = activity;
    }

    /// <summary>Handles <see cref="AdjustClientCreditCommand"/>.</summary>
    /// <param name="cmd">The adjustment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>CLIENT_NOT_FOUND</c> when the id names no client, or <c>CLIENT_CREDIT_INSUFFICIENT</c> when a deduction
    /// is more than the balance holds; nothing is saved on a refusal.
    /// </returns>
    public async Task<Result> HandleAsync(AdjustClientCreditCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var adding = cmd.Direction == CreditAdjustment.Add;
        var change = new ClientCreditChange(
            cmd.ClientId,
            cmd.Amount,
            adding ? CreditMovementType.AdminAdd : CreditMovementType.AdminDeduct,
            Note: cmd.Reason);
        var moved = adding
            ? await _clientCredit.AddAsync(change, ct)
            : await _clientCredit.DeductAsync(change, ct);
        if (moved.IsFailure)
        {
            return Result.FailureCode(moved.ErrorCode!);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(cmd.ClientId, $"{(adding ? "Added" : "Deducted")} Credit - Amount: {cmd.Amount}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
