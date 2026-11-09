using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Changes a client's credit balance for a module that does not own clients, so no module outside
/// Clients writes the clients table itself.
/// </summary>
/// <remarks>
/// The change joins the caller's unit of work: it goes through the client's own credit methods on
/// the scoped context and is saved by the caller's <c>SaveChangesAsync</c>, so moving credit
/// between an invoice and the balance commits or fails as one. A port rather than a Clients command
/// for that reason: a command dispatched through the bus would save on its own, and a failed
/// invoice save would leave the balance changed.
/// <para>
/// Every change also writes the movement to the client ledger in the same unit of work: the entry that already holds the
/// money (<see cref="ClientCreditChange.Entry"/>) is stamped with the movement, its signed change and the balance it left, and
/// a movement that moved no cash gets a credit-only entry of its own. That is the credit history, so a caller cannot move the
/// balance without saying why.
/// </para>
/// </remarks>
public interface IClientCredit
{
    /// <summary>Stages adding the amount to the client's balance.</summary>
    /// <param name="change">The client and the positive amount to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The balance once saved, or <c>CLIENT_NOT_FOUND</c> when the id names no client.</returns>
    public Task<Result<ClientCreditBalance>> AddAsync(ClientCreditChange change, CancellationToken ct);

    /// <summary>Stages taking the amount off the client's balance; never more than the balance holds.</summary>
    /// <param name="change">The client and the positive amount to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The balance once saved, <c>CLIENT_NOT_FOUND</c> when the id names no client, or
    /// <c>CLIENT_CREDIT_INSUFFICIENT</c> when the balance is short; a refusal stages nothing.
    /// </returns>
    public Task<Result<ClientCreditBalance>> DeductAsync(ClientCreditChange change, CancellationToken ct);

    /// <summary>
    /// Adds the amount to the client's balance and saves the caller's whole unit of work at once,
    /// for money that is already taken and has to land on the balance (a payment that cannot be
    /// recorded, a refund to credit after a failed fulfilment).
    /// </summary>
    /// <remarks>
    /// The client row carries an optimistic-concurrency token. When the save conflicts on the client
    /// alone, the client is reloaded and the addition applied once more on the fresh balance: an
    /// addition is the same whatever the balance was, so repeating it after the failed save (which
    /// wrote nothing) is safe. A second conflict, or a conflict on any other row, is not retried and
    /// reaches the caller as <c>ConcurrencyConflictException</c>.
    /// </remarks>
    /// <param name="change">The client and the positive amount to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved balance, or <c>CLIENT_NOT_FOUND</c> when the id names no client (nothing is saved then).</returns>
    public Task<Result<ClientCreditBalance>> AddAndSaveAsync(ClientCreditChange change, CancellationToken ct);
}
