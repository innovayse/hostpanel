using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetRegistrarAutoRenew;

/// <summary>
/// Turns automatic renewal on or off at the registrar that holds a domain, and nowhere else: the panel's own renewal
/// flag (<c>RegisteredDomain.AutoRenew</c>) is left as it is. The change is recorded in the owning client's activity
/// log, stamped with the caller.
/// </summary>
public sealed class SetRegistrarAutoRenewHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain is read through and the activity line is saved with.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<SetRegistrarAutoRenewHandler> _logger;

    /// <summary>Records the activity log line, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain is read through and the activity line is saved with.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    /// <param name="activity">Records the activity log line, saved with the change it describes.</param>
    public SetRegistrarAutoRenewHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<SetRegistrarAutoRenewHandler> logger,
        IActivityLogger activity)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
        _activity = activity;
    }

    /// <summary>Handles <see cref="SetRegistrarAutoRenewCommand"/>.</summary>
    /// <param name="cmd">The set registrar auto renew command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success; <c>DOMAIN_NOT_FOUND</c> when the id names no domain; <c>DOMAIN_NO_REGISTRAR_REF</c> when the domain was
    /// never placed at a registrar, so there is nothing to change there; or <c>DOMAIN_AUTO_RENEW_UPSTREAM_FAILED</c>
    /// when the registrar rejects the update.
    /// </returns>
    public async Task<Result> HandleAsync(SetRegistrarAutoRenewCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.RegistrarRef is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
        }

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

        await _activity.RecordAsync(
            new ActivityLogEntry(
                domain.ClientId,
                $"Set Domain Registrar Auto Renew - Domain ID: {domain.Id}, Registrar Auto Renew: {cmd.Value}"),
            ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
