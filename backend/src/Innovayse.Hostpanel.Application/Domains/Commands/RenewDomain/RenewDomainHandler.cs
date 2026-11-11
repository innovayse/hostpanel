using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.RenewDomain;

/// <summary>
/// Renews an existing domain registration at the registrar and updates the aggregate's expiry date.
/// </summary>
public sealed class RenewDomainHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<RenewDomainHandler> _logger;

    /// <summary>The clock that dates the renewal.</summary>
    private readonly IClock _clock;

    /// <summary>Writes the activity log line that shows staff a renewal the registrar may have made twice.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    /// <param name="clock">The clock that dates the renewal.</param>
    /// <param name="activityLogger">Writes the activity log line that shows staff a renewal the registrar may have made twice.</param>
    public RenewDomainHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<RenewDomainHandler> logger,
        IClock clock,
        IActivityLogger activityLogger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
        _clock = clock;
        _activityLogger = activityLogger;
    }

    /// <summary>
    /// Handles <see cref="RenewDomainCommand"/>.
    /// </summary>
    /// <param name="cmd">The renew domain command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DOMAIN_NO_REGISTRAR_REF</c> when the domain was never placed at a registrar, or
    /// <c>DOMAIN_RENEW_NOT_ALLOWED</c> when the domain is cancelled or transferred out, or
    /// <c>DOMAIN_RENEWAL_UPSTREAM_FAILED</c> when the registrar rejects the renewal.
    /// </returns>
    public async Task<Result> HandleAsync(RenewDomainCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .Include(x => x.LineRenewals)
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.RegistrarRef is null)
        {
            // Never placed at a registrar, so there is nothing to renew there. Extending the
            // expiry here would mark a paid domain renewed while nothing changed upstream.
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
        }

        if (!domain.CanRenew)
        {
            // Refused before the registrar is called, so a renewal the domain would refuse is never bought.
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_RENEW_NOT_ALLOWED);
        }

        var request = new DomainRenewalOrder(domain.Name, domain.RegistrarRef, cmd.Years);
        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.RenewAsync(request, ct);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar rejected renewal of '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DOMAIN_RENEWAL_UPSTREAM_FAILED));
        }

        var newExpiresAt = result.ExpiresAt ?? domain.ExpiresAt.AddYears(cmd.Years);

        domain.Renew(newExpiresAt, _clock.UtcNow, cmd.OrderItemId);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException) when (cmd.OrderItemId is not null)
        {
            var lineId = cmd.OrderItemId.Value;
            if (!await IsRenewedForLineAsync(domain.Id, lineId, ct))
            {
                // Not the line's renewal recorded by another attempt: an ordinary lost race, answered as before.
                throw;
            }

            // Another attempt of the same order line recorded its renewal first (the unique index on the line refused this
            // one). The line is renewed; the registrar was asked twice, which staff must look at, but the line's answer is
            // the same as the winner's.
            _logger.LogCritical(
                "Domain {DomainId} was renewed for order line {OrderItemId} by two attempts at once; the registrar may have " +
                "extended it twice. The line counts as renewed.",
                domain.Id, lineId);
            await RecordPossibleDoubleRenewalAsync(domain.Id, lineId, result.ExpiresAt, ct);
        }

        return Result.Success();
    }

    /// <summary>
    /// Makes a renewal the registrar may have made twice visible to staff: the domain's client log gets a line naming the
    /// domain and the order line, and when this losing attempt was told a later expiry than the winner stored, that later
    /// date is kept, because the registrar holds it. The failed changes of this attempt are dropped first, and the domain is
    /// read as the winner saved it.
    /// </summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="orderItemId">The order line both attempts renewed for.</param>
    /// <param name="reportedExpiresAt">The expiry the registrar told this attempt, or <see langword="null"/> when it gave none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the line and any later expiry are saved.</returns>
    private async Task RecordPossibleDoubleRenewalAsync(int domainId, int orderItemId, DateTimeOffset? reportedExpiresAt, CancellationToken ct)
    {
        _context.DetachAll();
        var stored = await _context.Domains.FirstAsync(d => d.Id == domainId, ct);
        var keptLater = reportedExpiresAt is { } reported && stored.AdoptLaterRegistrarExpiry(reported);
        await _activityLogger.RecordAsync(
            new ActivityLogEntry(
                stored.ClientId,
                $"Domain Possibly Renewed Twice - Domain: {stored.Name} - Order Line ID: {orderItemId} - The registrar may have " +
                $"renewed it twice; check the registration period at the registrar - Expiry: {stored.ExpiresAt:yyyy-MM-dd}" +
                (keptLater ? " (later expiry reported by the registrar kept)" : string.Empty)),
            ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Tells whether a renewal of the domain for the order line is on record, as the database holds it now.</summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="orderItemId">The order line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the line's renewal is recorded.</returns>
    private Task<bool> IsRenewedForLineAsync(int domainId, int orderItemId, CancellationToken ct)
    {
        return _context.Domains
            .AsNoTracking()
            .AnyAsync(d => d.Id == domainId && d.LineRenewals.Any(r => r.OrderItemId == orderItemId), ct);
    }
}
