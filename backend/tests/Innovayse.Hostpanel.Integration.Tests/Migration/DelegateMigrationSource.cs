using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// A source whose page answers come from a function, so a pager test can script exact replies:
/// a stuck keyset id, a page count with no keyset id, or a failure on a given request.
/// </summary>
/// <param name="answer">Answers one page request with the record ids of that page.</param>
internal sealed class DelegateMigrationSource(Func<MigrationPageSelection, MigrationRecordPage<long>> answer) : IMigrationSource
{
    /// <summary>Every page request, in order.</summary>
    public List<MigrationPageSelection> Requests { get; } = [];

    /// <inheritdoc/>
    public Task PingAsync(MigrationSourceEndpoint source, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<MigrationSourceTotals> GetTotalsAsync(MigrationSourceEndpoint source, CancellationToken ct) =>
        Task.FromResult(MigrationSourceTotals.Empty);

    /// <inheritdoc/>
    public Task<MigrationRecordPage<TRecord>> GetRecordPageAsync<TRecord>(
        MigrationSourceEndpoint source, MigrationPageSelection selection, CancellationToken ct)
    {
        Requests.Add(selection);
        var page = answer(selection);
        var items = page.Items?.Select(id => (TRecord)(object)id).ToList();
        return Task.FromResult(new MigrationRecordPage<TRecord>(items, page.TotalPages, page.NextAfterId));
    }
}
