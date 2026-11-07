using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Domain.Migration;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="MigrationRecordPager"/> against a source that pages by keyset: every row is read
/// once, in id order, by following the id each page names as the next start.
/// </summary>
public sealed class MigrationRecordPagerTests
{
    /// <summary>A keyset source is paged by the id it names, past the page count it reports.</summary>
    [Fact]
    public async Task PagesAsync_KeysetSource_ReadsEveryRowOnceAsync()
    {
        // 120 rows with gaps in the ids, more than two pages of the pager's page size.
        var rowIds = Enumerable.Range(1, 120).Select(i => (long)(i * 3)).ToList();
        var source = new KeysetMigrationSource(rowIds);
        var pager = new MigrationRecordPager(source);
        var job = MigrationJob.Create(new MigrationJobSetup(
            Label: "keyset",
            SourceUrl: "http://fake-source.invalid",
            Selection: new MigrationExportSelection(
                Clients: true, Invoices: false, Services: false, Domains: false, Tickets: false,
                Products: false, Orders: false, Transactions: false, Quotes: false, Knowledgebase: false,
                Contacts: false, TicketReplies: false, Announcements: false, Downloads: false, NetworkIssues: false)));

        var read = new List<long>();
        await foreach (var page in pager.PagesAsync<long>(job, "clients", CancellationToken.None))
        {
            read.AddRange(page);
        }

        Assert.Equal(rowIds, read);
        Assert.Equal(0, source.RequestedAfterIds[0]);
        Assert.True(source.RequestedAfterIds.Count > 2);
    }

    /// <summary>A source with no records is read with one request and yields no page.</summary>
    [Fact]
    public async Task PagesAsync_EmptySource_MakesOneRequestAndYieldsNothingAsync()
    {
        var source = new KeysetMigrationSource([]);

        var read = await ReadAllAsync(source);

        Assert.Empty(read);
        Assert.Equal([0L], source.RequestedAfterIds);
    }

    /// <summary>
    /// When the record count is an exact multiple of the page size, the last full page still names
    /// a next id; the empty page after it ends the read, and no record is read twice.
    /// </summary>
    [Fact]
    public async Task PagesAsync_ExactMultipleOfThePageSize_StopsOnTheEmptyPageAsync()
    {
        var rowIds = Enumerable.Range(1, 100).Select(i => (long)i).ToList();
        var source = new KeysetMigrationSource(rowIds);

        var read = await ReadAllAsync(source);

        Assert.Equal(rowIds, read);
        Assert.Equal([0L, 50L, 100L], source.RequestedAfterIds);
    }

    /// <summary>A next id that does not move forward ends the read instead of asking for the same page forever.</summary>
    [Fact]
    public async Task PagesAsync_NextIdThatDoesNotAdvance_StopsAsync()
    {
        var source = new DelegateMigrationSource(_ => new MigrationRecordPage<long>([1, 2, 3], 1, NextAfterId: 0));

        var read = await ReadAllAsync(source);

        Assert.Equal([1L, 2L, 3L], read);
        Assert.Single(source.Requests);
    }

    /// <summary>A source that names no keyset id is read by its page count, one page after another.</summary>
    [Fact]
    public async Task PagesAsync_NoKeysetIds_ReadsByThePageCountAsync()
    {
        var source = new DelegateMigrationSource(s => new MigrationRecordPage<long>([s.Page * 10L], TotalPages: 3));

        var read = await ReadAllAsync(source);

        Assert.Equal([10L, 20L, 30L], read);
        Assert.Equal([1, 2, 3], source.Requests.Select(r => r.Page));
    }

    /// <summary>A page that fails stops the read with the failure, after the pages before it were handed out.</summary>
    [Fact]
    public async Task PagesAsync_PageFails_ThrowsAfterTheEarlierPagesAsync()
    {
        var source = new DelegateMigrationSource(s => s.AfterId == 0
            ? new MigrationRecordPage<long>([.. Enumerable.Range(1, 50).Select(i => (long)i)], 2, NextAfterId: 50)
            : throw new HttpRequestException("The migration source answered 500."));
        var read = new List<long>();

        var act = async () =>
        {
            await foreach (var page in new MigrationRecordPager(source).PagesAsync<long>(Job(), "clients", CancellationToken.None))
            {
                read.AddRange(page);
            }
        };

        await act.Should().ThrowAsync<HttpRequestException>();
        Assert.Equal(50, read.Count);
    }

    /// <summary>Reads every page the pager yields for a clients job.</summary>
    /// <param name="source">The source to read from.</param>
    /// <returns>Every record read, in order.</returns>
    private static async Task<List<long>> ReadAllAsync(Innovayse.Hostpanel.Application.Common.Interfaces.IMigrationSource source)
    {
        var read = new List<long>();
        await foreach (var page in new MigrationRecordPager(source).PagesAsync<long>(Job(), "clients", CancellationToken.None))
        {
            read.AddRange(page);
        }

        return read;
    }

    /// <summary>Builds a job that selects clients only.</summary>
    /// <returns>The job.</returns>
    private static MigrationJob Job() => MigrationJob.Create(new MigrationJobSetup(
        Label: "pager",
        SourceUrl: "http://fake-source.invalid",
        Selection: new MigrationExportSelection(
            Clients: true, Invoices: false, Services: false, Domains: false, Tickets: false,
            Products: false, Orders: false, Transactions: false, Quotes: false, Knowledgebase: false,
            Contacts: false, TicketReplies: false, Announcements: false, Downloads: false, NetworkIssues: false)));
}
