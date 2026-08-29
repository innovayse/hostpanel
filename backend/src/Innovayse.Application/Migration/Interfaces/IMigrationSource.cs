namespace Innovayse.Application.Migration.Interfaces;

using Innovayse.Application.Migration.Common;

/// <summary>
/// The legacy system a migration job pulls its data out of, described the way the migration
/// needs it: something reachable with the job's key, that can say how much it holds, and that
/// hands its records over one page at a time.
/// <para>
/// Implemented in Infrastructure. How the source is actually addressed — the protocol, the
/// request shape, the timeout — is that implementation's business and never the Application
/// layer's; nothing here names a transport.
/// </para>
/// <para>
/// Every method throws when the source is unreachable, refuses the key, or answers an error.
/// A migration job treats such a throw as the failure of the whole pull and records the
/// exception's message against the job, so an implementation's message is what an operator reads.
/// </para>
/// </summary>
public interface IMigrationSource
{
    /// <summary>
    /// Checks that the source answers and accepts the job's key, without pulling anything.
    /// This is what the admin UI's connection test asks for.
    /// </summary>
    /// <param name="sourceUrl">Where the source listens, as configured on the job.</param>
    /// <param name="accessKey">The job's shared key; the source refuses anything else.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the source has answered successfully.</returns>
    Task PingAsync(string sourceUrl, string accessKey, CancellationToken ct = default);

    /// <summary>
    /// Asks the source how many records of each kind it holds, so the job can size its
    /// progress counters before the first page is pulled.
    /// </summary>
    /// <param name="sourceUrl">Where the source listens, as configured on the job.</param>
    /// <param name="accessKey">The job's shared key; the source refuses anything else.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The per-kind counts the source reports.</returns>
    Task<MigrationSourceTotals> GetTotalsAsync(
        string sourceUrl,
        string accessKey,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches one page of records of a single kind.
    /// </summary>
    /// <typeparam name="TRecord">Shape the caller expects each record on the page to have.</typeparam>
    /// <param name="sourceUrl">Where the source listens, as configured on the job.</param>
    /// <param name="accessKey">The job's shared key; the source refuses anything else.</param>
    /// <param name="dataSet">
    /// Which kind of record to fetch, in the source's own vocabulary — <c>clients</c>,
    /// <c>invoices</c>, <c>ticket_replies</c> and so on. The source decides the spelling;
    /// the migration only passes it through.
    /// </param>
    /// <param name="page">Which page to fetch, counting from 1.</param>
    /// <param name="perPage">How many records the page should hold at most.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The records on this page and how many pages the source holds in total.</returns>
    Task<MigrationRecordPage<TRecord>> GetRecordPageAsync<TRecord>(
        string sourceUrl,
        string accessKey,
        string dataSet,
        int page,
        int perPage,
        CancellationToken ct = default);
}
