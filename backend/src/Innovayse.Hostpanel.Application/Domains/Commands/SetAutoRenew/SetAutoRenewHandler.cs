using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetAutoRenew;

/// <summary>
/// Enables or disables automatic renewal for a domain at both the registrar and aggregate levels.
/// The change is recorded in the owning client's activity log, saved together with it; the line is stamped with the
/// caller, so a staff change and a client's own change both show who made them.
/// </summary>
public sealed class SetAutoRenewHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<SetAutoRenewHandler> _logger;

    /// <summary>Records the activity log line, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    /// <param name="activity">Records the activity log line, saved with the change it describes.</param>
    public SetAutoRenewHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<SetAutoRenewHandler> logger,
        IActivityLogger activity)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
        _activity = activity;
    }

    /// <summary>
    /// Handles <see cref="SetAutoRenewCommand"/>.
    /// </summary>
    /// <param name="cmd">The set auto-renew command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DOMAIN_AUTO_RENEW_UPSTREAM_FAILED</c> when the registrar rejects the update.
    /// </returns>
    public async Task<Result> HandleAsync(SetAutoRenewCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.RegistrarRef is not null)
        {
            var registrar = await _registrars.ResolveAsync(domain, ct);
            var result = await registrar.SetAutoRenewAsync(
                new RegistrarFlagChange(new DomainRef(domain.Name, domain.RegistrarRef), cmd.Value!.Value), ct);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Registrar failed to update auto-renew for '{DomainName}': {Reason}",
                    domain.Name, result.ErrorCode);
                return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DOMAIN_AUTO_RENEW_UPSTREAM_FAILED));
            }
        }

        domain.SetAutoRenew(cmd.Value!.Value);
        await _activity.RecordAsync(
            new ActivityLogEntry(domain.ClientId, $"Set Domain Auto Renew - Domain ID: {domain.Id}, Auto Renew: {cmd.Value}"), ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
