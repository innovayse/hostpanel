using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
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

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    /// <param name="clock">The clock that dates the renewal.</param>
    public RenewDomainHandler(RegistrarProviderResolver registrars, IHostpanelDbContext context, ILogger<RenewDomainHandler> logger, IClock clock)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
        _clock = clock;
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
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
