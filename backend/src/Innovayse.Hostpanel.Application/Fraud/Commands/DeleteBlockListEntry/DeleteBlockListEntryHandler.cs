using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.DeleteBlockListEntry;

/// <summary>Removes a block list entry.</summary>
public sealed class DeleteBlockListEntryHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteBlockListEntryHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The entry to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>FRAUD_BLOCKLIST_ENTRY_NOT_FOUND</c> when the id names no entry.</returns>
    public async Task<Result> HandleAsync(DeleteBlockListEntryCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entry = await _context.BlockListEntries.FirstOrDefaultAsync(e => e.Id == cmd.Id, ct);
        if (entry is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.FRAUD_BLOCKLIST_ENTRY_NOT_FOUND);
        }

        _context.BlockListEntries.Remove(entry);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
