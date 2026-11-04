using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// A source that serves one dataset by keyset paging, like the real addon: each record gets the id
/// of its position (1, 2, 3, ...), a full page names its last id as the next start, and a short
/// page is the last one. It can fail the page that starts after a given id, once, the way a source
/// that drops one request mid-pull answers.
/// </summary>
/// <param name="dataSet">The dataset name the records are served under, e.g. <c>clients</c>.</param>
/// <param name="records">The records, one JSON object each.</param>
internal sealed class PagedMigrationPullSource(string dataSet, IReadOnlyList<string> records) : IMigrationSource
{
    /// <summary>The id after which the next page request fails once with HTTP 500; null fails nothing.</summary>
    public long? FailOnceAfterId { get; set; }

    /// <summary>The start ids of every page request, in order.</summary>
    public List<long> RequestedAfterIds { get; } = [];

    /// <inheritdoc/>
    public Task PingAsync(MigrationSourceEndpoint source, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<MigrationSourceTotals> GetTotalsAsync(MigrationSourceEndpoint source, CancellationToken ct) =>
        Task.FromResult(MigrationSourceTotals.Empty);

    /// <inheritdoc/>
    public Task<MigrationRecordPage<TRecord>> GetRecordPageAsync<TRecord>(
        MigrationSourceEndpoint source, MigrationPageSelection selection, CancellationToken ct)
    {
        RequestedAfterIds.Add(selection.AfterId);
        if (FailOnceAfterId == selection.AfterId)
        {
            FailOnceAfterId = null;
            throw new HttpRequestException("The migration source answered 500.", inner: null, HttpStatusCode.InternalServerError);
        }

        if (!string.Equals(selection.DataSet, dataSet, StringComparison.Ordinal))
        {
            return Task.FromResult(new MigrationRecordPage<TRecord>([], 0));
        }

        var start = (int)selection.AfterId;
        var slice = records.Skip(start).Take(selection.PerPage).ToList();
        var items = slice.Select(json => JsonSerializer.Deserialize<TRecord>(json)!).ToList();
        long? nextAfterId = slice.Count == selection.PerPage ? start + slice.Count : null;
        var totalPages = (int)Math.Ceiling(records.Count / (double)selection.PerPage);
        return Task.FromResult(new MigrationRecordPage<TRecord>(items, totalPages, nextAfterId));
    }
}
