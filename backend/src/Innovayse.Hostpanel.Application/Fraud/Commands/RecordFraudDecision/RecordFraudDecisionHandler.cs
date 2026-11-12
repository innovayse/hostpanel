using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;

/// <summary>Appends one decision to the fraud log.</summary>
public sealed class RecordFraudDecisionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public RecordFraudDecisionHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The decision to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new log entry's id. Never refuses.</returns>
    public async Task<Result<int>> HandleAsync(RecordFraudDecisionCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entry = FraudLogEntry.Create(
            cmd.Kind, cmd.Decision, cmd.ClientId, cmd.OrderId, cmd.IpAddress, cmd.Email, cmd.Score, cmd.Reasons);
        _context.FraudLogEntries.Add(entry);
        await _context.SaveChangesAsync(ct);

        return Result.Success(entry.Id);
    }
}
