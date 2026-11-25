using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetAutoRenew;

/// <summary>
/// Enables or disables the panel's own automatic renewal for a domain: whether the panel raises renewal invoices and
/// renews it. The registrar is never asked; its auto renew is a separate setting (<c>SetRegistrarAutoRenew</c>).
/// The change is recorded in the owning client's activity log, saved together with it; the line is stamped with the
/// caller, so a staff change and a client's own change both show who made them.
/// </summary>
public sealed class SetAutoRenewHandler
{
    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="activity">Records the activity log line, saved with the change it describes.</param>
    public SetAutoRenewHandler(IHostpanelDbContext context, IActivityLogger activity)
    {
        _context = context;
        _activity = activity;
    }

    /// <summary>
    /// Handles <see cref="SetAutoRenewCommand"/>.
    /// </summary>
    /// <param name="cmd">The set auto-renew command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain.</returns>
    public async Task<Result> HandleAsync(SetAutoRenewCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains.FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        domain.SetAutoRenew(cmd.Value!.Value);
        await _activity.RecordAsync(
            new ActivityLogEntry(domain.ClientId, $"Set Domain Auto Renew - Domain ID: {domain.Id}, Auto Renew: {cmd.Value}"), ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
