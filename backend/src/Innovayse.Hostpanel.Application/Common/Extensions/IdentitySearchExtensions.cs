using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>
/// The one way a list searches what only the identity provider holds about a person (the sign-in address and the
/// name behind an account): it asks the provider which subjects match, so the list can keep the rows of those
/// subjects next to its own text columns (<see cref="TextSearchExtensions.SearchIncludingIdOrMatching{T}"/>).
/// </summary>
public static class IdentitySearchExtensions
{
    /// <summary>
    /// How many accounts one search draws on. The answer becomes an <c>IN</c> clause, so a search that matches more
    /// people than this would build a query the database has to be talked into accepting; the list is then drawn
    /// from the first matches instead, and the person narrows the search.
    /// </summary>
    private const int MatchLimit = 500;

    /// <summary>The subjects of the accounts whose address or name contains the text, in any letter case.</summary>
    /// <param name="identity">The identity provider to ask.</param>
    /// <param name="term">What the person typed; blank means no search and matches nobody.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching subjects, at most one search's worth, or none when the term is blank.</returns>
    public static async Task<IReadOnlyList<string>> FindSubjectsAsync(
        this IIdentityProvider identity, string? term, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var matches = await identity.ListAsync(new IdentityAccountListQuery(term.Trim(), 1, MatchLimit), ct);

        return matches.Items.Select(m => m.Subject).ToList();
    }
}
