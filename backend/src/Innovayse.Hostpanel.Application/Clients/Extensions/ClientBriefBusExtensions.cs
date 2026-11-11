using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClientBriefs;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// The shapes other modules ask <see cref="GetClientBriefsQuery"/> in, so each caller dispatches
/// the query the same way instead of reading the Clients tables.
/// </summary>
public static class ClientBriefBusExtensions
{
    /// <summary>One client record by id.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="clientId">The client record's id.</param>
    /// <param name="withEmail"><see langword="true"/> to resolve the owner's sign-in address and language too.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The brief, or <see langword="null"/> when the id names no record.</returns>
    public static async Task<ClientBriefDto?> FindClientBriefAsync(
        this IMessageBus bus, int clientId, bool withEmail, CancellationToken ct)
    {
        var briefs = await SendAsync(bus, new GetClientBriefsQuery(ClientIds: [clientId], WithEmail: withEmail), ct);

        return briefs.FirstOrDefault();
    }

    /// <summary>
    /// The client record that a row held by a restricting foreign key names (an invoice, a quote, a
    /// transaction, a billable item): the database refuses to delete a client while such a row exists.
    /// </summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="clientId">The client record's id, read from the row.</param>
    /// <param name="withEmail"><see langword="true"/> to resolve the owner's sign-in address and language too.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The brief.</returns>
    /// <exception cref="InvalidOperationException">The id names no record, so the database is broken.</exception>
    public static async Task<ClientBriefDto> GetRequiredClientBriefAsync(
        this IMessageBus bus, int clientId, bool withEmail, CancellationToken ct)
    {
        return await bus.FindClientBriefAsync(clientId, withEmail, ct) ?? throw MissingClient(clientId);
    }

    /// <summary>
    /// The client records that rows held by a restricting foreign key name, for a list that shows a
    /// name per row; see <see cref="GetRequiredClientBriefAsync"/>.
    /// </summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="clientIds">The client record ids, read from the rows; duplicates are answered once.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every requested record keyed by id.</returns>
    /// <exception cref="InvalidOperationException">An id names no record, so the database is broken.</exception>
    public static async Task<IReadOnlyDictionary<int, ClientBriefDto>> GetRequiredClientBriefsByIdAsync(
        this IMessageBus bus, IReadOnlyList<int> clientIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clientIds);

        var briefs = await bus.GetClientBriefsByIdAsync(clientIds, ct);
        foreach (var clientId in clientIds)
        {
            if (!briefs.ContainsKey(clientId))
            {
                throw MissingClient(clientId);
            }
        }

        return briefs;
    }

    /// <summary>Whether a client record with this id exists.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="clientId">The client record's id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the id names a record.</returns>
    public static async Task<bool> ClientExistsAsync(this IMessageBus bus, int clientId, CancellationToken ct)
    {
        return await bus.FindClientBriefAsync(clientId, withEmail: false, ct) is not null;
    }

    /// <summary>The client record a person owns, by their identity subject.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="userSubject">The owner's identity subject.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The brief, or <see langword="null"/> when the person owns no record.</returns>
    public static async Task<ClientBriefDto?> FindClientBriefBySubjectAsync(
        this IMessageBus bus, string userSubject, CancellationToken ct)
    {
        var briefs = await SendAsync(bus, new GetClientBriefsQuery(UserSubjects: [userSubject]), ct);

        return briefs.FirstOrDefault();
    }

    /// <summary>Many client records by id, for a list that shows a name per row.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="clientIds">The client record ids; duplicates are answered once.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The known records keyed by id; an unknown id is absent.</returns>
    public static Task<IReadOnlyDictionary<int, ClientBriefDto>> GetClientBriefsByIdAsync(
        this IMessageBus bus, IReadOnlyList<int> clientIds, CancellationToken ct)
    {
        return bus.GetClientBriefsByIdAsync(clientIds, withEmail: false, ct);
    }

    /// <summary>Many client records by id, for a caller that mails them and so needs the sign-in address and language.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="clientIds">The client record ids; duplicates are answered once.</param>
    /// <param name="withEmail">Whether the sign-in address and language are read from the identity provider too.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The known records keyed by id; an unknown id is absent.</returns>
    public static async Task<IReadOnlyDictionary<int, ClientBriefDto>> GetClientBriefsByIdAsync(
        this IMessageBus bus, IReadOnlyList<int> clientIds, bool withEmail, CancellationToken ct)
    {
        var briefs = await SendAsync(bus, new GetClientBriefsQuery(ClientIds: clientIds, WithEmail: withEmail), ct);

        return briefs.ToDictionary(b => b.Id);
    }

    /// <summary>Many client records by their owners' identity subjects.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="userSubjects">The owners' subjects; duplicates are answered once.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The known records keyed by subject; a subject that owns no record is absent.</returns>
    public static async Task<IReadOnlyDictionary<string, ClientBriefDto>> GetClientBriefsBySubjectAsync(
        this IMessageBus bus, IReadOnlyList<string> userSubjects, CancellationToken ct)
    {
        var briefs = await SendAsync(bus, new GetClientBriefsQuery(UserSubjects: userSubjects), ct);

        return briefs.ToDictionary(b => b.UserSubject, StringComparer.Ordinal);
    }

    /// <summary>The failure for a client id that a restricting foreign key should have kept alive.</summary>
    /// <param name="clientId">The client record id that names no record.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException MissingClient(int clientId)
    {
        return new InvalidOperationException(
            $"Client {clientId} does not exist, yet a row whose foreign key restricts deleting it names it.");
    }

    /// <summary>Dispatches the query and unwraps its answer.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="query">The query to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The briefs, or none when a key failed validation.</returns>
    private static async Task<IReadOnlyList<ClientBriefDto>> SendAsync(
        IMessageBus bus, GetClientBriefsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = await bus.InvokeAsync<Result<IReadOnlyList<ClientBriefDto>>>(query, ct);

        return result.Value ?? [];
    }
}
