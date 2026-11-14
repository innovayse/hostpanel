using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetDomainRenewalEmails;

/// <summary>Handles <see cref="GetDomainRenewalEmailsQuery"/>.</summary>
public sealed class GetDomainRenewalEmailsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetDomainRenewalEmailsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The filters to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every matching renewal reminder, most recently sent first. Never refuses.</returns>
    public async Task<Result<DomainRenewalEmailsDto>> HandleAsync(GetDomainRenewalEmailsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var reminders =
            from rem in _context.DomainReminders.AsNoTracking()
            join d in _context.Domains.AsNoTracking() on rem.DomainId equals d.Id
            join c in _context.Clients.AsNoTracking() on d.ClientId equals c.Id
            select new { rem, d, c };

        if (query.ClientId.HasValue)
        {
            reminders = reminders.Where(x => x.d.ClientId == query.ClientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Registrar))
        {
            reminders = reminders.Where(x => x.d.Registrar != null && x.d.Registrar.ToLower() == query.Registrar.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(query.Domain))
        {
            reminders = reminders.Where(x => x.d.Name.ToLower().Contains(query.Domain.ToLower()));
        }

        if (query.From.HasValue)
        {
            var fromDto = query.From.Value.StartOfDayUtc();
            reminders = reminders.Where(x => x.rem.SentAt >= fromDto);
        }

        if (query.To.HasValue)
        {
            var toDto = query.To.Value.EndOfDayExclusiveUtc();
            reminders = reminders.Where(x => x.rem.SentAt < toDto);
        }

        var raw = await reminders
            .OrderByDescending(x => x.rem.SentAt)
            .ToListAsync(ct);

        IReadOnlyList<DomainRenewalEmailRowDto> rows = raw.Select(x => new DomainRenewalEmailRowDto(
            x.c.FirstName + " " + x.c.LastName,
            x.d.Name,
            x.rem.SentAt,
            x.rem.ReminderType,
            x.rem.SentTo)).ToList();

        return Result<DomainRenewalEmailsDto>.Success(new DomainRenewalEmailsDto(rows));
    }
}
