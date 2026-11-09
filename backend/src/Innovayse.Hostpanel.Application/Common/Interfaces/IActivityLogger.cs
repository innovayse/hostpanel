using Innovayse.Hostpanel.Application.Audit.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Records a line in the activity log (a client's, or a system-wide one with no client) for the
/// module that owns the action, so no module outside Audit writes the activity log table itself.
/// </summary>
/// <remarks>
/// The line joins the caller's unit of work: it is stamped with the current caller (id, name,
/// email, IP) and saved by the caller's own <c>SaveChangesAsync</c>, so the audit line and the
/// change it describes commit or fail together. A port rather than an Audit command for that
/// reason: a command dispatched through the bus would save on its own and could record an action
/// whose change was then rolled back.
/// </remarks>
public interface IActivityLogger
{
    /// <summary>Stages the line for the caller's next save.</summary>
    /// <param name="entry">The client and the description of what happened.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the line is staged; nothing is saved yet.</returns>
    public Task RecordAsync(ActivityLogEntry entry, CancellationToken ct);
}
