using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Commands.MarkDomainExpired;

/// <summary>
/// Transitions an active domain to the expired state and raises the <c>DomainExpiredEvent</c> domain event.
/// Typically invoked by a scheduled job that checks for overdue expirations.
/// </summary>
public sealed class MarkDomainExpiredHandler
{
    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock that dates the expiry.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="clock">The clock that dates the expiry.</param>
    public MarkDomainExpiredHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Handles <see cref="MarkDomainExpiredCommand"/>.
    /// </summary>
    /// <param name="cmd">The mark domain expired command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain.</returns>
    public async Task<Result> HandleAsync(MarkDomainExpiredCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        domain.MarkExpired(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
