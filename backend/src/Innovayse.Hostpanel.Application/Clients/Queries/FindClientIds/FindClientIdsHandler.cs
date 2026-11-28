using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.FindClientIds;

/// <summary>
/// Handles <see cref="FindClientIdsQuery"/>: the clients whose own name or company contains the text, together with the
/// clients of the accounts whose sign-in address or name the identity provider says contain it.
/// </summary>
public sealed class FindClientIdsHandler
{
    /// <summary>
    /// How many clients one search answers with. The caller turns the answer into an <c>IN</c> clause, so a search that
    /// matches more clients than this is drawn from the first of them and the person narrows the text.
    /// </summary>
    private const int MatchLimit = 500;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Finds the accounts whose address or name matches, which the client row does not hold.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Finds the accounts whose address or name matches, which the client row does not hold.</param>
    public FindClientIdsHandler(IHostpanelDbContext context, IIdentityProvider identity)
    {
        _context = context;
        _identity = identity;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The text to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the matching clients, lowest first, at most one search's worth. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<int>>> HandleAsync(FindClientIdsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var subjects = await _identity.FindSubjectsAsync(query.Search, ct);
        var clients = _context.Clients.AsNoTracking();

        var byName = clients
            .Search(query.Search, ClientSearchExtensions.SearchColumns)
            .Select(c => c.Id);
        var byAccount = clients.Where(c => subjects.Contains(c.UserId)).Select(c => c.Id);

        IReadOnlyList<int> ids = await byName.Union(byAccount).OrderBy(id => id).Take(MatchLimit).ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(ids);
    }
}
