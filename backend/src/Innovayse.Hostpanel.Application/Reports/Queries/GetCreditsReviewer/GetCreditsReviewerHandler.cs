using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetCreditsReviewer;

/// <summary>Handles <see cref="GetCreditsReviewerQuery"/>.</summary>
public sealed class GetCreditsReviewerHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetCreditsReviewerHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The filters and the page to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The count and net sum of the matching credit movements per currency, summed in SQL, and one page of them,
    /// newest first. Never refuses.
    /// </returns>
    public async Task<Result<CreditsReviewerDto>> HandleAsync(GetCreditsReviewerQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Every movement of a client's balance is a credit here: a paid top-up, a credit note, staff goodwill, credit applied to an
        // invoice or handed back (each recorded on the ledger with its signed change), and the older rows that put cash on the
        // balance before the history existed (added to credit, no movement recorded). The amount is the signed change.
        var credits = _context.Transactions
            .AsNoTracking()
            .Where(t => t.CreditMovement != null || (t.AddedToCredit && t.AmountIn > 0));

        if (query.ClientId.HasValue)
        {
            var clientId = query.ClientId.Value;
            credits = credits.Where(t => t.ClientId == clientId);
        }

        if (query.From.HasValue)
        {
            var from = query.From.Value.StartOfDayUtc();
            credits = credits.Where(t => t.Date >= from);
        }

        if (query.To.HasValue)
        {
            var toExclusive = query.To.Value.EndOfDayExclusiveUtc();
            credits = credits.Where(t => t.Date < toExclusive);
        }

        if (query.MinAmount.HasValue)
        {
            var min = query.MinAmount.Value;
            credits = credits.Where(t => (t.CreditChange ?? t.AmountIn) >= min);
        }

        if (query.MaxAmount.HasValue)
        {
            var max = query.MaxAmount.Value;
            credits = credits.Where(t => (t.CreditChange ?? t.AmountIn) <= max);
        }

        var totals = await credits
            .GroupBy(t => t.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new CreditsReviewerCurrencyTotalDto(g.Key, g.Count(), g.Sum(t => t.CreditChange ?? t.AmountIn)))
            .ToListAsync(ct);

        var rows = await credits
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Paginate(query.Page, query.PageSize)
            .Join(_context.Clients.AsNoTracking(), t => t.ClientId, c => c.Id, (t, c) => new CreditsReviewerRowDto(
                t.Id,
                c.Id,
                c.FirstName + " " + c.LastName,
                t.Date.ToString("yyyy-MM-dd"),
                t.Description,
                t.Currency,
                t.CreditChange ?? t.AmountIn))
            .ToListAsync(ct);

        var page = new PaginatedList<CreditsReviewerRowDto>(rows, totals.Sum(t => t.TotalCount), query.Page, query.PageSize);
        return Result<CreditsReviewerDto>.Success(new CreditsReviewerDto(totals, page));
    }
}
