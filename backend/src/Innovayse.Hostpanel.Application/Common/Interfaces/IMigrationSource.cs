using Innovayse.Hostpanel.Application.Migration.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The legacy system a migration job pulls its data out of, described the way the migration
/// needs it: something reachable with the job's key, that can say how much it holds, and that
/// hands its records over one page at a time.
/// <para>
/// Implemented in Infrastructure. How the source is actually addressed, the protocol, the
/// request shape, the timeout, is that implementation's business and never the Application
/// layer's; nothing here names a transport.
/// </para>
/// <para>
/// Every method throws when the source is unreachable, refuses the key, or answers an error.
/// A migration job treats such a throw as the failure of the whole pull and records a closed
/// reason for it (<see cref="Innovayse.Hostpanel.Domain.Migration.Enums.MigrationJobFailure"/>), never the exception's text.
/// A source address this server may not call is reported as <see cref="MigrationSourceAddressRefusedException"/>.
/// </para>
/// </summary>
public interface IMigrationSource
{
    /// <summary>
    /// Checks that the source answers and accepts the job's key, without pulling anything.
    /// This is what the admin UI's connection test asks for.
    /// </summary>
    /// <param name="source">Where the source listens and the key it accepts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the source has answered successfully.</returns>
    public Task PingAsync(MigrationSourceEndpoint source, CancellationToken ct);

    /// <summary>
    /// Asks the source how many records of each kind it holds, so the job can size its
    /// progress counters before the first page is pulled.
    /// </summary>
    /// <param name="source">Where the source listens and the key it accepts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The per-kind counts the source reports.</returns>
    public Task<MigrationSourceTotals> GetTotalsAsync(MigrationSourceEndpoint source, CancellationToken ct);

    /// <summary>
    /// Fetches one page of records of a single kind.
    /// </summary>
    /// <typeparam name="TRecord">Shape the caller expects each record on the page to have.</typeparam>
    /// <param name="source">Where the source listens and the key it accepts.</param>
    /// <param name="selection">The kind of record and the page of it to fetch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The records on this page and how many pages the source holds in total.</returns>
    public Task<MigrationRecordPage<TRecord>> GetRecordPageAsync<TRecord>(
        MigrationSourceEndpoint source, MigrationPageSelection selection, CancellationToken ct);
}
