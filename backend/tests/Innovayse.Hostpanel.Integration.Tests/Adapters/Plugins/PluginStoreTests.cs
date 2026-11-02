using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginStore"/> against Postgres: values are separate per plugin, per client reference and plugin wide,
/// a key is one row however often and however concurrently it is written, keys and values outside the rules are refused
/// without a write or a leak, and a write saves through the request's context so another context sees it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginStoreTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The keys the store refuses, as the domain rule lists them.</summary>
    /// <returns>The keys that are refused.</returns>
    public static TheoryData<string?> InvalidKeys() => new()
    {
        null,
        string.Empty,
        " ",
        new string('a', PluginDataEntry.MaxKeyLength + 1),
        "a b",
        "a/b",
        "a;b",
        "é",
        "a\n",
    };

    /// <summary>A plugin id that no other test uses.</summary>
    /// <returns>The id.</returns>
    private static string NewPlugin() => $"store-{Guid.NewGuid():N}";

    /// <summary>A client reference that no other test uses, in the shape of the real ones.</summary>
    /// <returns>The reference.</returns>
    private static string NewRef() => Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray())
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>Builds a store over a context.</summary>
    /// <param name="db">The context.</param>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientRef">The client reference, or <see langword="null"/> for plugin wide.</param>
    /// <returns>The store.</returns>
    private static PluginStore StoreOf(AppDbContext db, string pluginId, string? clientRef)
    {
        return new PluginStore(pluginId, clientRef, db);
    }

    /// <summary>Reads the rows of a plugin straight from the table.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The rows.</returns>
    private async Task<List<PluginDataEntry>> RowsAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataEntries.AsNoTracking().Where(row => row.PluginId == pluginId).ToListAsync();
    }

    /// <summary>Two plugins with the same key and client keep separate values, and removing one leaves the other.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoPlugins_SameKeyAndClient_AreIndependent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (a, b, clientRef) = (NewPlugin(), NewPlugin(), NewRef());
        var storeA = StoreOf(db, a, clientRef);
        var storeB = StoreOf(db, b, clientRef);

        await storeA.SetAsync("k", "from-a", CancellationToken.None);
        await storeB.SetAsync("k", "from-b", CancellationToken.None);

        (await storeA.GetAsync("k", CancellationToken.None)).Should().Be("from-a");
        (await storeB.GetAsync("k", CancellationToken.None)).Should().Be("from-b");
        (await storeA.RemoveAsync("k", CancellationToken.None)).Should().BeTrue();
        (await storeA.GetAsync("k", CancellationToken.None)).Should().BeNull();
        (await storeB.GetAsync("k", CancellationToken.None)).Should().Be("from-b");
    }

    /// <summary>Two clients of one plugin keep separate values and cannot read each other's.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoClients_SamePluginAndKey_AreIndependent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, x, y) = (NewPlugin(), NewRef(), NewRef());
        var storeX = StoreOf(db, plugin, x);
        var storeY = StoreOf(db, plugin, y);

        await storeX.SetAsync("k", "of-x", CancellationToken.None);
        (await storeY.GetAsync("k", CancellationToken.None)).Should().BeNull();
        await storeY.SetAsync("k", "of-y", CancellationToken.None);

        (await storeX.GetAsync("k", CancellationToken.None)).Should().Be("of-x");
        (await storeY.GetAsync("k", CancellationToken.None)).Should().Be("of-y");
        (await storeY.RemoveAsync("k", CancellationToken.None)).Should().BeTrue();
        (await storeX.GetAsync("k", CancellationToken.None)).Should().Be("of-x");
    }

    /// <summary>A plugin-wide value and a per-client value of the same key are two rows that do not see each other.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PluginWideAndPerClient_AreIndependent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var wide = StoreOf(db, plugin, null);
        var perClient = StoreOf(db, plugin, clientRef);

        await wide.SetAsync("k", "wide", CancellationToken.None);
        (await perClient.GetAsync("k", CancellationToken.None)).Should().BeNull();
        await perClient.SetAsync("k", "client", CancellationToken.None);

        (await wide.GetAsync("k", CancellationToken.None)).Should().Be("wide");
        (await perClient.GetAsync("k", CancellationToken.None)).Should().Be("client");
        (await RowsAsync(plugin)).Should().HaveCount(2);
        (await wide.RemoveAsync("k", CancellationToken.None)).Should().BeTrue();
        (await perClient.GetAsync("k", CancellationToken.None)).Should().Be("client");
    }

    /// <summary>Writing a plugin-wide key twice leaves one row with the second value, an UpdatedAt set by the second write and the first CreatedAt.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SettingAPluginWideKeyTwice_KeepsOneRowAndMovesUpdatedAt()
    {
        await AssertSecondSetReplacesAsync(null);
    }

    /// <summary>Writing a per-client key twice leaves one row with the second value, an UpdatedAt set by the second write and the first CreatedAt.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SettingAPerClientKeyTwice_KeepsOneRowAndMovesUpdatedAt()
    {
        await AssertSecondSetReplacesAsync(NewRef());
    }

    /// <summary>An unset key reads as null, a remove says whether it removed something, and empty and non-ASCII text round-trip.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetRemoveAndRoundTrips_BehaveAsTheSdkDocuments()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = StoreOf(db, NewPlugin(), NewRef());
        const string Text = "Հայերեն éü 中文 \U0001F600\U0001F680";

        (await store.GetAsync("missing", CancellationToken.None)).Should().BeNull();
        (await store.RemoveAsync("missing", CancellationToken.None)).Should().BeFalse();

        await store.SetAsync("empty", string.Empty, CancellationToken.None);
        await store.SetAsync("text", Text, CancellationToken.None);

        (await store.GetAsync("empty", CancellationToken.None)).Should().Be(string.Empty);
        (await store.GetAsync("text", CancellationToken.None)).Should().Be(Text);
        (await store.RemoveAsync("text", CancellationToken.None)).Should().BeTrue();
        (await store.RemoveAsync("text", CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>A key outside the rules is refused by every method with PLUGIN_STORE_KEY_INVALID, nothing is written and the message does not echo the key.</summary>
    /// <param name="key">The refused key.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(InvalidKeys))]
    public async Task AnInvalidKey_IsRefusedEverywhereAndWritesNothing(string? key)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = NewPlugin();
        var store = StoreOf(db, plugin, NewRef());

        var get = (await FluentActions.Awaiting(() => store.GetAsync(key!, CancellationToken.None)).Should().ThrowAsync<DomainRuleException>()).Which;
        var set = (await FluentActions.Awaiting(() => store.SetAsync(key!, "v", CancellationToken.None)).Should().ThrowAsync<DomainRuleException>()).Which;
        var remove = (await FluentActions.Awaiting(() => store.RemoveAsync(key!, CancellationToken.None)).Should().ThrowAsync<DomainRuleException>()).Which;

        (await RowsAsync(plugin)).Should().BeEmpty();
        foreach (var error in new[] { get, set, remove })
        {
            error.Code.Should().Be(PluginStoreErrors.KeyInvalid);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var error in new[] { get, set, remove })
            {
                error.Message.Should().NotContain(key);
            }
        }
    }

    /// <summary>A value of exactly the cap is stored in full.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AValueAtTheCap_IsAccepted()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = StoreOf(db, NewPlugin(), NewRef());
        var value = new string('x', PluginDataEntry.MaxValueLength);

        await store.SetAsync("big", value, CancellationToken.None);

        (await store.GetAsync("big", CancellationToken.None)).Should().Be(value);
    }

    /// <summary>A value over the cap and a missing value are refused with PLUGIN_STORE_VALUE_INVALID and nothing is written.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnOversizedOrMissingValue_IsRefusedAndWritesNothing()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = NewPlugin();
        var store = StoreOf(db, plugin, NewRef());
        var oversized = new string('x', PluginDataEntry.MaxValueLength + 1);

        var big = (await FluentActions.Awaiting(() => store.SetAsync("big", oversized, CancellationToken.None)).Should().ThrowAsync<DomainRuleException>()).Which;
        var missing = (await FluentActions.Awaiting(() => store.SetAsync("none", null!, CancellationToken.None)).Should().ThrowAsync<DomainRuleException>()).Which;

        big.Code.Should().Be(PluginStoreErrors.ValueInvalid);
        big.Message.Should().NotContain(oversized);
        missing.Code.Should().Be(PluginStoreErrors.ValueInvalid);
        (await RowsAsync(plugin)).Should().BeEmpty();
    }

    /// <summary>
    /// Twenty writers of one key, each on its own context, leave one row holding one of the written values: the first
    /// writes race on the unique index and the losers write over the winner.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ParallelWritersOfOneKey_LeaveOneRow()
    {
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var values = Enumerable.Range(0, 20).Select(i => $"value-{i}").ToList();

        await Task.WhenAll(values.Select(async value =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await StoreOf(db, plugin, clientRef).SetAsync("race", value, CancellationToken.None);
        }));

        var rows = await RowsAsync(plugin);
        rows.Should().ContainSingle();
        values.Should().Contain(rows[0].Value);
    }

    /// <summary>A value written through one store instance is read by a second store built over a fresh scope's context.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AValueWrittenByOneStore_IsReadByAnotherOverAFreshContext()
    {
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        using (var writing = factory.Services.CreateScope())
        {
            var db = writing.ServiceProvider.GetRequiredService<AppDbContext>();
            await StoreOf(db, plugin, clientRef).SetAsync("k", "v", CancellationToken.None);
        }

        using var reading = factory.Services.CreateScope();
        var other = reading.ServiceProvider.GetRequiredService<AppDbContext>();

        (await StoreOf(other, plugin, clientRef).GetAsync("k", CancellationToken.None)).Should().Be("v");
        (await RowsAsync(plugin)).Should().ContainSingle();
    }

    /// <summary>A remove through a fresh scope's context deletes a row another scope wrote, and the key then reads as null.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARemoveThroughAFreshContext_DeletesTheRow()
    {
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        using (var writing = factory.Services.CreateScope())
        {
            var db = writing.ServiceProvider.GetRequiredService<AppDbContext>();
            await StoreOf(db, plugin, clientRef).SetAsync("k", "v", CancellationToken.None);
        }

        using (var removing = factory.Services.CreateScope())
        {
            var db = removing.ServiceProvider.GetRequiredService<AppDbContext>();
            (await StoreOf(db, plugin, clientRef).RemoveAsync("k", CancellationToken.None)).Should().BeTrue();
        }

        (await RowsAsync(plugin)).Should().BeEmpty();
        using var reading = factory.Services.CreateScope();
        var other = reading.ServiceProvider.GetRequiredService<AppDbContext>();
        (await StoreOf(other, plugin, clientRef).GetAsync("k", CancellationToken.None)).Should().BeNull();
    }

    /// <summary>Sets one key twice, a minute apart on a clock the test moves, and checks the row.</summary>
    /// <param name="clientRef">The client reference, or <see langword="null"/> for a plugin-wide value.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private async Task AssertSecondSetReplacesAsync(string? clientRef)
    {
        using var host = new ClockedHost(factory);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = NewPlugin();
        var store = StoreOf(db, plugin, clientRef);

        await store.SetAsync("k", "first", CancellationToken.None);
        var afterFirst = (await RowsAsync(plugin)).Should().ContainSingle().Subject;
        afterFirst.UpdatedAt.Should().BeNull();
        afterFirst.CreatedAt.Should().NotBe(default);

        host.Clock.Advance(TimeSpan.FromMinutes(1));
        await store.SetAsync("k", "second", CancellationToken.None);

        var row = (await RowsAsync(plugin)).Should().ContainSingle().Subject;
        row.Value.Should().Be("second");
        row.CreatedAt.Should().Be(afterFirst.CreatedAt);
        row.UpdatedAt.Should().NotBeNull();
        row.UpdatedAt.Should().BeAfter(row.CreatedAt);
        row.ClientRef.Should().Be(clientRef);
    }
}
