using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Events;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PurgeRemovedPluginDataHandler"/> reached the way production reaches it, by publishing
/// <see cref="PluginRemovedEvent"/> on the real bus: it deletes, scope by scope, every store entry of the removed plugin
/// last written at or before the removal and every scope row it empties, across several batches; it keeps rows written
/// or rewritten after the removal together with their scope, whose totals still describe them; it leaves another
/// plugin's rows alone; and a second delivery, sequential or concurrent, finds nothing and completes.
/// </summary>
/// <remarks>
/// Every seeded scope row is opened and fed through <see cref="PluginDataScope.Admit"/> for each entry it holds,
/// exactly as the store writes them, because the purge releases each entry from its scope and that release refuses a
/// scope whose totals do not cover the row. The removal moment is taken from the rows' own stamps (the latest
/// <c>CreatedAt</c> or <c>UpdatedAt</c> of the rows that predate it), so no test clock is needed to place a row before
/// or after it.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PurgeRemovedPluginDataTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>More rows in one scope than the handler loads in one batch (500), so the purge runs several batches.</summary>
    private const int RowsOverOneBatch = 1201;

    /// <summary>A quota no seed reaches: the seeds measure the totals, they do not test the quota.</summary>
    private static readonly PluginStoreQuota Unbounded = new(int.MaxValue, long.MaxValue);

    /// <summary>
    /// Every scope of the removed plugin is purged (plugin wide and two clients, entries and scope rows) and another
    /// plugin's entries and scope rows are left with their totals.
    /// </summary>
    [Fact]
    public async Task Publish_PluginWithSeveralScopes_RemovesOnlyThatPluginsRowsAsync()
    {
        var pluginId = NewPluginId();
        var otherPluginId = NewPluginId();
        await SeedAsync(pluginId, null, new SeedRow("settings", "wide"));
        await SeedAsync(pluginId, "client-a", new SeedRow("cursor", "1"));
        await SeedAsync(pluginId, "client-b", new SeedRow("cursor", "2"));
        await SeedAsync(otherPluginId, null, new SeedRow("settings", "kept"));
        await SeedAsync(otherPluginId, "client-a", new SeedRow("cursor", "kept"));
        var removedAt = await LatestWriteAsync(pluginId);

        await factory.Services.PublishAndWaitAsync(new PluginRemovedEvent(pluginId, removedAt));

        (await EntryCountAsync(pluginId)).Should().Be(0);
        (await ScopeCountAsync(pluginId)).Should().Be(0, "every emptied scope row is deleted");
        (await EntryCountAsync(otherPluginId)).Should().Be(2, "another plugin's entries are not touched");
        (await ScopeCountAsync(otherPluginId)).Should().Be(2, "another plugin's scope rows are not touched");
        await AssertScopesMatchEntriesAsync(otherPluginId);
    }

    /// <summary>A scope holding more entries than one batch has every one of them removed, then the scope row.</summary>
    [Fact]
    public async Task Publish_MoreRowsThanOneBatchInOneScope_RemovesAllOfThemAsync()
    {
        var pluginId = NewPluginId();
        await SeedAsync(pluginId, "client-a", ManyRows());
        await SeedAsync(pluginId, null, new SeedRow("settings", "wide"));
        (await EntryCountAsync(pluginId)).Should().Be(RowsOverOneBatch + 1);
        await AssertScopesMatchEntriesAsync(pluginId);
        var removedAt = await LatestWriteAsync(pluginId);

        await factory.Services.PublishAndWaitAsync(new PluginRemovedEvent(pluginId, removedAt));

        (await EntryCountAsync(pluginId)).Should().Be(0);
        (await ScopeCountAsync(pluginId)).Should().Be(0);
    }

    /// <summary>
    /// Rows written after the removal (a new key in a scope that also held old rows, and a whole new scope) survive,
    /// the scope that keeps a newer row is not deleted, and its totals equal exactly the rows left in it; a scope
    /// holding only old rows is emptied and deleted.
    /// </summary>
    [Fact]
    public async Task Publish_RowsWrittenAfterTheRemoval_AreKeptWithTheirScopeAsync()
    {
        var pluginId = NewPluginId();
        await SeedAsync(pluginId, "client-a", new SeedRow("old-1", "before"), new SeedRow("old-2", "before the removal"));
        await SeedAsync(pluginId, "client-b", new SeedRow("old", "before"));
        var removedAt = await LatestWriteAsync(pluginId);
        await SeedAsync(pluginId, "client-a", new SeedRow("new", "written by the reinstalled plugin"));
        await SeedAsync(pluginId, "client-c", new SeedRow("new", "a scope opened after the removal"));
        (await EarliestWriteAfterAsync(pluginId, removedAt))
            .Should().BeAfter(removedAt, "the newer rows are stamped later");

        await factory.Services.PublishAndWaitAsync(new PluginRemovedEvent(pluginId, removedAt));

        (await KeysAsync(pluginId, "client-a"))
            .Should().Equal(new[] { "new" }, "only the row written after the removal is kept");
        (await KeysAsync(pluginId, "client-b")).Should().BeEmpty();
        (await KeysAsync(pluginId, "client-c")).Should().Equal("new");
        (await ScopeClientRefsAsync(pluginId)).Should().BeEquivalentTo(
            new string?[] { "client-a", "client-c" },
            "a scope that keeps a newer row stays and the emptied one is deleted");
        await AssertScopesMatchEntriesAsync(pluginId);
    }

    /// <summary>
    /// An old key whose value was rewritten after the removal counts as written after it (its <c>UpdatedAt</c>), so it
    /// is kept with its scope, while the old key beside it is purged.
    /// </summary>
    [Fact]
    public async Task Publish_RowRewrittenAfterTheRemoval_IsKeptAsync()
    {
        var pluginId = NewPluginId();
        await SeedAsync(pluginId, null, new SeedRow("rewritten", "old"), new SeedRow("untouched", "old"));
        var removedAt = await LatestWriteAsync(pluginId);
        await RewriteAsync(pluginId, null, "rewritten", "a longer value written after the removal");

        await factory.Services.PublishAndWaitAsync(new PluginRemovedEvent(pluginId, removedAt));

        (await KeysAsync(pluginId, null)).Should().Equal("rewritten");
        (await ScopeCountAsync(pluginId)).Should().Be(1);
        await AssertScopesMatchEntriesAsync(pluginId);
    }

    /// <summary>
    /// More than one batch of old rows sharing a scope with a newer row: every old row goes, the scope and the newer
    /// row stay.
    /// </summary>
    [Fact]
    public async Task Publish_MoreOldRowsThanOneBatchBesideANewerRow_KeepsTheScopeAsync()
    {
        var pluginId = NewPluginId();
        await SeedAsync(pluginId, "client-a", ManyRows());
        var removedAt = await LatestWriteAsync(pluginId);
        await SeedAsync(pluginId, "client-a", new SeedRow("new", "after"));

        await factory.Services.PublishAndWaitAsync(new PluginRemovedEvent(pluginId, removedAt));

        (await KeysAsync(pluginId, "client-a")).Should().Equal("new");
        (await ScopeCountAsync(pluginId)).Should().Be(1);
        await AssertScopesMatchEntriesAsync(pluginId);
    }

    /// <summary>
    /// A second delivery of the same removal finds nothing old and completes, leaving a newer row and its scope as
    /// they were.
    /// </summary>
    [Fact]
    public async Task Publish_Twice_TheSecondDeliveryIsANoOpAsync()
    {
        var pluginId = NewPluginId();
        await SeedAsync(pluginId, null, new SeedRow("settings", "wide"));
        await SeedAsync(pluginId, "client-a", new SeedRow("old", "before"));
        var removedAt = await LatestWriteAsync(pluginId);
        await SeedAsync(pluginId, "client-a", new SeedRow("new", "after"));

        await factory.Services.PublishAndWaitAsync(new PluginRemovedEvent(pluginId, removedAt));
        var second = async () => await factory.Services.PublishAndWaitAsync(new PluginRemovedEvent(pluginId, removedAt));

        await second.Should().NotThrowAsync();
        (await KeysAsync(pluginId, null)).Should().BeEmpty();
        (await KeysAsync(pluginId, "client-a")).Should().Equal("new");
        (await ScopeClientRefsAsync(pluginId)).Should().BeEquivalentTo(new string?[] { "client-a" });
        await AssertScopesMatchEntriesAsync(pluginId);
    }

    /// <summary>
    /// Two deliveries purging the same plugin at the same time both complete without a fault: a batch that loses to
    /// the other delivery's change of the scope row is detached and the scope is read again. Run on two scopes of their
    /// own, because the tracked publish waits for one session at a time.
    /// </summary>
    [Fact]
    public async Task HandleAsync_TwoConcurrentDeliveries_BothCompleteAndNothingIsLeftAsync()
    {
        var pluginId = NewPluginId();
        await SeedAsync(pluginId, null, ManyRows());
        var removedAt = await LatestWriteAsync(pluginId);

        await Task.WhenAll(PurgeDirectlyAsync(pluginId, removedAt), PurgeDirectlyAsync(pluginId, removedAt));

        (await EntryCountAsync(pluginId)).Should().Be(0);
        (await ScopeCountAsync(pluginId)).Should().Be(0);
    }

    /// <summary>The keys of one scope holding more rows than one purge batch, all with the same short value.</summary>
    /// <returns><see cref="RowsOverOneBatch"/> distinct keys and their values.</returns>
    private static SeedRow[] ManyRows()
    {
        return Enumerable.Range(0, RowsOverOneBatch).Select(i => new SeedRow($"key-{i}", "value")).ToArray();
    }

    /// <summary>Makes a plugin id no other test uses, in the shape the plugin id rule accepts.</summary>
    /// <returns>A lower-case id with a hyphen.</returns>
    private static string NewPluginId()
    {
        return $"purge-{Guid.NewGuid():N}";
    }

    /// <summary>Runs the handler once in a scope of its own, with that scope's context.</summary>
    /// <param name="pluginId">The plugin whose rows are purged.</param>
    /// <param name="removedAt">The removal moment the event carries.</param>
    /// <returns>A task that completes when the handler returns.</returns>
    private async Task PurgeDirectlyAsync(string pluginId, DateTimeOffset removedAt)
    {
        using var scope = factory.Services.CreateScope();
        var handler = new PurgeRemovedPluginDataHandler(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            scope.ServiceProvider.GetRequiredService<ILogger<PurgeRemovedPluginDataHandler>>());

        await handler.HandleAsync(new PluginRemovedEvent(pluginId, removedAt), CancellationToken.None);
    }

    /// <summary>
    /// Writes new keys into one scope the way the store does: the scope row is loaded (or opened with its first write),
    /// each entry is admitted into its totals, and everything is saved at once.
    /// </summary>
    /// <param name="pluginId">The plugin whose scope is written.</param>
    /// <param name="clientRef">The scope's client reference, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <param name="rows">The new keys and their values; none may exist in the scope yet.</param>
    /// <returns>A task that completes once the rows and the scope are saved.</returns>
    private async Task SeedAsync(string pluginId, string? clientRef, params SeedRow[] rows)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dataScope = await context.PluginDataScopes
            .FirstOrDefaultAsync(s => s.PluginId == pluginId && s.ClientRef == clientRef);
        if (dataScope is null)
        {
            dataScope = PluginDataScope.Open(pluginId, clientRef);
            context.PluginDataScopes.Add(dataScope);
        }

        foreach (var (key, value) in rows)
        {
            dataScope.Admit(1, PluginDataEntry.CharsOf(key, value), Unbounded);
            context.PluginDataEntries.Add(PluginDataEntry.Create(pluginId, clientRef, key, value));
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Replaces one stored value the way the store does, moving its scope's characters by the change.</summary>
    /// <param name="pluginId">The plugin whose value is replaced.</param>
    /// <param name="clientRef">The scope's client reference, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <param name="key">The existing key.</param>
    /// <param name="value">The new value.</param>
    /// <returns>A task that completes once the change is saved (stamping the row's <c>UpdatedAt</c>).</returns>
    private async Task RewriteAsync(string pluginId, string? clientRef, string key, string value)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dataScope = await context.PluginDataScopes
            .SingleAsync(s => s.PluginId == pluginId && s.ClientRef == clientRef);
        var entry = await context.PluginDataEntries
            .SingleAsync(e => e.PluginId == pluginId && e.ClientRef == clientRef && e.Key == key);
        dataScope.Admit(0, PluginDataEntry.CharsOf(key, value) - entry.StoredChars, Unbounded);
        entry.ChangeValue(value);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// The latest moment any row of the plugin was written, which serves as the removal moment of the rows seeded so far.
    /// </summary>
    /// <param name="pluginId">The plugin whose rows are read.</param>
    /// <returns>The latest <c>UpdatedAt</c> or <c>CreatedAt</c> among the plugin's entries.</returns>
    private async Task<DateTimeOffset> LatestWriteAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PluginDataEntries.AsNoTracking()
            .Where(e => e.PluginId == pluginId)
            .MaxAsync(e => e.UpdatedAt ?? e.CreatedAt);
    }

    /// <summary>The earliest write among the plugin's rows written after a moment, proving a seed really is later.</summary>
    /// <param name="pluginId">The plugin whose rows are read.</param>
    /// <param name="after">The moment the rows are compared with.</param>
    /// <returns>The earliest <c>UpdatedAt</c> or <c>CreatedAt</c> later than <paramref name="after"/>.</returns>
    private async Task<DateTimeOffset> EarliestWriteAfterAsync(string pluginId, DateTimeOffset after)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PluginDataEntries.AsNoTracking()
            .Where(e => e.PluginId == pluginId && (e.UpdatedAt ?? e.CreatedAt) > after)
            .MinAsync(e => e.UpdatedAt ?? e.CreatedAt);
    }

    /// <summary>
    /// Asserts that every scope row of the plugin counts exactly the entries left in it: its key count and stored
    /// characters equal those of its rows, and no entry is left without a scope.
    /// </summary>
    /// <param name="pluginId">The plugin whose scopes are checked.</param>
    /// <returns>A task that completes once the assertion has run.</returns>
    private async Task AssertScopesMatchEntriesAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scopes = await context.PluginDataScopes.AsNoTracking().Where(s => s.PluginId == pluginId).ToListAsync();
        var entries = await context.PluginDataEntries.AsNoTracking().Where(e => e.PluginId == pluginId).ToListAsync();

        foreach (var dataScope in scopes)
        {
            var own = entries.Where(e => e.ClientRef == dataScope.ClientRef).ToList();
            dataScope.KeyCount.Should().Be(
                own.Count, "scope {0} counts its own rows", dataScope.ClientRef ?? "(plugin wide)");
            dataScope.StoredChars.Should().Be(own.Sum(e => e.StoredChars));
        }

        entries.Select(e => e.ClientRef).Distinct()
            .Should().BeSubsetOf(scopes.Select(s => s.ClientRef), "every kept row stays in a scope that counts it");
    }

    /// <summary>The keys of one scope, in key order, read in a fresh scope.</summary>
    /// <param name="pluginId">The plugin whose keys are read.</param>
    /// <param name="clientRef">The scope's client reference, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <returns>The keys the scope still holds.</returns>
    private async Task<List<string>> KeysAsync(string pluginId, string? clientRef)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PluginDataEntries.AsNoTracking()
            .Where(e => e.PluginId == pluginId && e.ClientRef == clientRef)
            .OrderBy(e => e.Key)
            .Select(e => e.Key)
            .ToListAsync();
    }

    /// <summary>The client references of the plugin's remaining scope rows.</summary>
    /// <param name="pluginId">The plugin whose scope rows are read.</param>
    /// <returns>One client reference (or <see langword="null"/>) per scope row.</returns>
    private async Task<List<string?>> ScopeClientRefsAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PluginDataScopes.AsNoTracking()
            .Where(s => s.PluginId == pluginId)
            .Select(s => s.ClientRef)
            .ToListAsync();
    }

    /// <summary>Counts the store entries of one plugin in a fresh scope, so nothing is read from a tracked copy.</summary>
    /// <param name="pluginId">The plugin whose entries are counted.</param>
    /// <returns>How many entries the plugin keeps.</returns>
    private async Task<int> EntryCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PluginDataEntries.AsNoTracking().CountAsync(e => e.PluginId == pluginId);
    }

    /// <summary>Counts the store scope rows of one plugin in a fresh scope.</summary>
    /// <param name="pluginId">The plugin whose scope rows are counted.</param>
    /// <returns>How many scope rows the plugin keeps.</returns>
    private async Task<int> ScopeCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PluginDataScopes.AsNoTracking().CountAsync(s => s.PluginId == pluginId);
    }

    /// <summary>One key and its value to seed into a scope.</summary>
    /// <param name="Key">The store key, in the shape the store accepts.</param>
    /// <param name="Value">The text kept under the key.</param>
    private sealed record SeedRow(string Key, string Value);
}
