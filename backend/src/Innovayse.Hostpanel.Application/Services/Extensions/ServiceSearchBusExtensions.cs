using Innovayse.Hostpanel.Application.Services.Queries.FindServiceIdsBySearch;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Extensions;

/// <summary>
/// The shape other handlers ask <see cref="FindServiceIdsBySearchQuery"/> in, so each list that keeps the rows of the
/// services a search text names dispatches the lookup the same way.
/// </summary>
public static class ServiceSearchBusExtensions
{
    /// <summary>
    /// The ids of the services a list's search text names (by own domain, linked domain, product name, client or
    /// service id), for a list that shows or refers to a service per row.
    /// </summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="term">What the person typed; blank means no search and matches no service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching service ids, at most one search's worth, or none when the term is blank.</returns>
    public static Task<IReadOnlyList<int>> FindServiceIdsMatchingAsync(
        this IMessageBus bus, string? term, CancellationToken ct)
    {
        return SendAsync(bus, term, query => new FindServiceIdsBySearchQuery(query), ct);
    }

    /// <summary>
    /// The ids of the services a list's search text names, for a list that may be scoped to one client: a scoped list
    /// holds only that client's services, so the client's name is not searched.
    /// </summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="term">What the person typed; blank means no search and matches no service.</param>
    /// <param name="scopedClientId">The client the list is scoped to, or <see langword="null"/> for every client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching service ids, at most one search's worth, or none when the term is blank.</returns>
    public static Task<IReadOnlyList<int>> FindServiceIdsMatchingAsync(
        this IMessageBus bus, string? term, int? scopedClientId, CancellationToken ct)
    {
        return SendAsync(bus, term, query => new FindServiceIdsBySearchQuery(query, scopedClientId), ct);
    }

    /// <summary>Dispatches the lookup for a non-blank term and unwraps its answer.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="term">What the person typed.</param>
    /// <param name="build">Builds the query from the trimmed term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching service ids, or none when the term is blank.</returns>
    private static async Task<IReadOnlyList<int>> SendAsync(
        IMessageBus bus, string? term, Func<string, FindServiceIdsBySearchQuery> build, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(build(term.Trim()), ct);

        return result.Value ?? [];
    }
}
