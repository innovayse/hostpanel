using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Plugins.Events;

/*
 * Why this is its own handler of PluginDataPurgeRequestedEvent, and why it stops at the moment of the removal.
 *
 * The operator's slot widget product bindings are data the host keeps for a plugin, so the operator purge removes them
 * with the plugin store. They are a second handler of the same event, not a step inside the store purge, so each is
 * retried and fails on its own and neither knows the other's tables. Like the store purge it returns Task, which gives
 * it a durable queue and durable retries; exceptions propagate.
 *
 * A retry can run hours after the purge was asked for, and the plugin may have been installed and configured again by
 * then. Only rows written at or before OccurredAt are deleted, so a binding saved after it stays.
 */

/// <summary>
/// Deletes the <c>PluginSlotProductBindings</c> rows of a plugin whose data is purged, those written at or before the
/// moment the purge was asked for. Idempotent: a repeat finds nothing old and stops.
/// </summary>
public sealed class PurgePluginSlotBindingsHandler
{
    /// <summary>The data context, owner of the binding table.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records how many bindings the purge deleted.</summary>
    private readonly ILogger<PurgePluginSlotBindingsHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context, owner of the binding table.</param>
    /// <param name="logger">Records how many bindings the purge deleted.</param>
    public PurgePluginSlotBindingsHandler(IHostpanelDbContext context, ILogger<PurgePluginSlotBindingsHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Deletes the plugin slot widget product bindings written up to the purge request.</summary>
    /// <param name="evt">The purge request, naming the plugin and the moment it was asked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once no binding of the plugin from before the request is left.</returns>
    public async Task HandleAsync(PluginDataPurgeRequestedEvent evt, CancellationToken ct)
    {
        var bindings = await _context.PluginSlotProductBindings
            .Where(b => b.PluginId == evt.PluginId && b.CreatedAt <= evt.OccurredAt)
            .ToListAsync(ct);
        _context.PluginSlotProductBindings.RemoveRange(bindings);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Purged {BindingCount} slot widget product bindings of plugin {PluginId}.", bindings.Count, evt.PluginId);
    }
}
