using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetClientBriefs;

/// <summary>
/// Handles <see cref="GetClientBriefsQuery"/>: reads the named client records in one pass and, when
/// asked, resolves their owners' sign-in addresses in one identity lookup.
/// </summary>
public sealed class GetClientBriefsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the owners' identity accounts, for their sign-in addresses.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Resolves the owners' identity accounts, for their sign-in addresses.</param>
    public GetClientBriefsHandler(IHostpanelDbContext context, IIdentityProvider identity)
    {
        _context = context;
        _identity = identity;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client records to describe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One brief per record that exists, each once, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ClientBriefDto>>> HandleAsync(GetClientBriefsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<int> ids = query.ClientIds ?? [];
        IReadOnlyList<string> subjects = query.UserSubjects ?? [];
        if (ids.Count == 0 && subjects.Count == 0)
        {
            return Result<IReadOnlyList<ClientBriefDto>>.Success([]);
        }

        var clients = await _context.Clients
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id) || subjects.Contains(c.UserId))
            .Select(c => new { c.Id, c.UserId, c.FirstName, c.LastName, c.CompanyName, c.Street, c.Address2, c.City, c.State, c.PostCode, c.Country, c.Currency, c.Status, c.NotifyInvoice, c.OverdueNotices, c.TaxExempt, c.TaxId, c.TaxExemptionReason, c.SeparateInvoices, c.DisableCcProcessing, c.MarketingOptIn, c.StatusUpdate, c.AllowSso, c.CreditBalance, c.CreatedAt })
            .ToListAsync(ct);

        IReadOnlyDictionary<string, IdentityAccount> accounts = query.WithEmail && clients.Count > 0
            ? await _identity.GetAccountsBySubjectsAsync(clients.Select(c => c.UserId), ct)
            : new Dictionary<string, IdentityAccount>(StringComparer.Ordinal);

        IReadOnlyList<ClientBriefDto> briefs = clients
            .Select(c => new ClientBriefDto(
                c.Id,
                c.UserId,
                c.FirstName,
                c.LastName,
                c.CompanyName,
                new ClientAddressDto(c.Street, c.Address2, c.City, c.State, c.PostCode, c.Country),
                accounts.TryGetValue(c.UserId, out var account) ? account.Email : null,
                account?.Language,
                c.Currency,
                c.Status,
                c.NotifyInvoice,
                new ClientSettingsDto(
                    c.OverdueNotices, c.TaxExempt, c.TaxId, c.TaxExemptionReason, c.SeparateInvoices, c.DisableCcProcessing,
                    c.MarketingOptIn, c.StatusUpdate, c.AllowSso),
                c.CreditBalance,
                c.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<ClientBriefDto>>.Success(briefs);
    }
}
