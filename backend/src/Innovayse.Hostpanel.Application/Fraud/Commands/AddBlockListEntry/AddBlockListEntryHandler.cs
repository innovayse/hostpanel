using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.AddBlockListEntry;

/// <summary>Adds a block list entry unless the same value is already listed for the type.</summary>
public sealed class AddBlockListEntryHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public AddBlockListEntryHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The entry to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new entry's id, or <c>FRAUD_BLOCKLIST_ENTRY_EXISTS</c> when the value is listed already for that type.</returns>
    public async Task<Result<int>> HandleAsync(AddBlockListEntryCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // The entry normalises the value, so the duplicate check compares the spelling that is stored.
        var entry = BlockListEntry.Create(cmd.Type, cmd.Value, cmd.Note);
        var exists = await _context.BlockListEntries.AnyAsync(e => e.Type == entry.Type && e.Value == entry.Value, ct);
        if (exists)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.FRAUD_BLOCKLIST_ENTRY_EXISTS);
        }

        _context.BlockListEntries.Add(entry);
        await _context.SaveChangesAsync(ct);

        return Result.Success(entry.Id);
    }
}
