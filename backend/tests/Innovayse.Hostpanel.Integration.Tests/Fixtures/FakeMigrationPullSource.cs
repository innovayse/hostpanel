using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IMigrationSource"/> standing in for the legacy install's own HTTP endpoint, built
/// for the per-importer tests under <c>Migration/Importers/</c>. Unlike
/// <see cref="FakeMigrationSource"/> it also counts how many times each dataset was paged, which
/// the idempotent-re-import tests assert on.
/// </summary>
/// <remarks>
/// Every record shape an importer pages through is a <c>private sealed record</c> nested inside
/// its own importer class, so a test in this assembly cannot name any of them. It does not need
/// to: <see cref="GetRecordPageAsync{TRecord}"/> deserializes each dataset's JSON into whatever
/// <c>TRecord</c> the calling importer's own generic instantiation supplies, exactly the way an
/// HTTP client deserializing the real plugin's response would.
/// </remarks>
public sealed class FakeMigrationPullSource : IMigrationSource
{
    /// <summary>One JSON array of records per dataset name, set by <see cref="SetDatasetJson"/>.</summary>
    private readonly Dictionary<string, string> _datasetsByName = new(StringComparer.Ordinal);

    /// <summary>How many single-page fetches each dataset has answered, keyed by dataset name.</summary>
    private readonly Dictionary<string, int> _fetchCountByDataset = new(StringComparer.Ordinal);

    /// <summary>The totals <see cref="GetTotalsAsync"/> answers with. Every count starts at zero.</summary>
    public MigrationSourceTotals Totals { get; set; } = MigrationSourceTotals.Empty;

    /// <summary>How many times <see cref="PingAsync"/> was called.</summary>
    public int PingCallCount { get; private set; }

    /// <summary>
    /// Seeds one dataset's single page of records as a raw JSON array. A test cannot construct the
    /// importer's own record type directly (every one is a <c>private sealed record</c> nested
    /// inside its importer) so the caller writes the array literally, matching the record's
    /// positional parameter names exactly: <see cref="JsonSerializer"/> matches case-sensitively by
    /// default, and a positional record's generated properties are PascalCase.
    /// </summary>
    /// <param name="dataset">The dataset name an importer pages through, e.g. <c>"tickets"</c>.</param>
    /// <param name="json">The dataset's one and only page, as a JSON array of records.</param>
    public void SetDatasetJson(string dataset, string json)
    {
        _datasetsByName[dataset] = json;
    }

    /// <summary>How many single-page fetches a dataset has answered so far.</summary>
    /// <param name="dataset">The dataset name to report the fetch count for.</param>
    /// <returns>The number of times <see cref="GetRecordPageAsync{TRecord}"/> was asked for that dataset.</returns>
    public int FetchCountFor(string dataset) => _fetchCountByDataset.GetValueOrDefault(dataset);

    /// <inheritdoc/>
    public Task PingAsync(MigrationSourceEndpoint source, CancellationToken ct)
    {
        PingCallCount++;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs once, the first time the totals are asked for, before they are answered. A test sets it to
    /// make something happen between a pull reading its queued job and starting it, such as a second
    /// pull of the same job. It is cleared before it runs, so a pull it starts reads the totals plainly.
    /// </summary>
    public Func<Task>? BeforeFirstTotals { get; set; }

    /// <inheritdoc/>
    public async Task<MigrationSourceTotals> GetTotalsAsync(MigrationSourceEndpoint source, CancellationToken ct)
    {
        var hook = BeforeFirstTotals;
        BeforeFirstTotals = null;
        if (hook is not null)
        {
            await hook();
        }

        return Totals;
    }

    /// <inheritdoc/>
    public Task<MigrationRecordPage<TRecord>> GetRecordPageAsync<TRecord>(
        MigrationSourceEndpoint source, MigrationPageSelection selection, CancellationToken ct)
    {
        var (dataSet, page, _, _) = selection;
        _fetchCountByDataset[dataSet] = _fetchCountByDataset.GetValueOrDefault(dataSet) + 1;

        // Every seeded dataset here is exactly one page; a second page always ends the pull.
        if (page > 1 || !_datasetsByName.TryGetValue(dataSet, out var json))
        {
            return Task.FromResult(new MigrationRecordPage<TRecord>([], 0));
        }

        var items = JsonSerializer.Deserialize<List<TRecord>>(json) ?? [];
        return Task.FromResult(new MigrationRecordPage<TRecord>(items, 1));
    }
}
