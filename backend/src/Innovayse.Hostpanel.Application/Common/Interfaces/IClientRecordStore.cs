using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Writes a client record for a user at most once, however many writers race to do it.
/// </summary>
/// <remarks>
/// A user gets a client record from two independent writers: the handler that reacts to
/// registration, which runs on the message bus after the register request has returned, and the
/// first order, which creates the record itself when it finds none. A fast caller can reach the
/// second before the first has run, and both then insert a row for the same user. The unique
/// index on the user decides which one wins; the loser must not fail, because the record it
/// wanted exists. Telling that conflict apart from any other failed save means knowing the
/// database's error, so it lives behind this port rather than in the handlers.
/// <para>
/// The same holds for a client linked to an external account: two members of one account ordering at the
/// same moment race on the unique index over the account, and the loser answers with the row that won.
/// </para>
/// </remarks>
public interface IClientRecordStore
{
    /// <summary>
    /// Saves the candidate record, or returns the one another writer saved first for the same user.
    /// </summary>
    /// <param name="candidate">The new client record, not yet added to the context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The saved candidate with <see cref="ClientRecordWriteResult.Created"/> true, or the record
    /// that already existed for the user with it false. Any other failure to save is thrown as it was.
    /// </returns>
    Task<ClientRecordWriteResult> AddOrGetAsync(Client candidate, CancellationToken ct);
}
