using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.PurgePluginData;

/// <summary>
/// Handles <see cref="PurgePluginDataCommand"/>: deletes every plugin store row of one plugin, in batches.
/// </summary>
public sealed class PurgePluginDataHandler
{
    /// <summary>How many rows one batch loads, removes and saves.</summary>
    private const int BatchSize = 500;

    /// <summary>The module's data context, holding the plugin store rows.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, holding the plugin store rows.</param>
    public PurgePluginDataHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Deletes every store row of the plugin, whatever its scope.</summary>
    /// <param name="cmd">Names the plugin whose rows are deleted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, also when the plugin had no rows.</returns>
    public async Task<Result> HandleAsync(PurgePluginDataCommand cmd, CancellationToken ct)
    {
        while (true)
        {
            // Batches, because a plugin may hold up to MaxStoreKeysPerScope rows per client, so loading them all
            // tracked in one query could be a very large set.
            var batch = await _context.PluginDataEntries
                .Where(e => e.PluginId == cmd.PluginId)
                .OrderBy(e => e.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                return Result.Success();
            }

            _context.PluginDataEntries.RemoveRange(batch);
            await _context.SaveChangesAsync(ct);
        }
    }
}
