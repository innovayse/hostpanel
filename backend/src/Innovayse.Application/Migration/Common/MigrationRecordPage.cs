namespace Innovayse.Application.Migration.Common;

/// <summary>
/// One page of records of a single kind, as the legacy source hands them over.
/// </summary>
/// <typeparam name="TRecord">Shape the caller expects each record on this page to have.</typeparam>
/// <param name="Items">
/// The records on this page. Null or empty where the source had nothing to give for this page —
/// the two mean the same thing here, and neither ends the paging on its own.
/// </param>
/// <param name="TotalPages">
/// How many pages the source holds in total for this kind of record. Zero means the source
/// reported no pages at all, which ends the paging immediately.
/// </param>
public sealed record MigrationRecordPage<TRecord>(
    IReadOnlyList<TRecord>? Items,
    int TotalPages);
