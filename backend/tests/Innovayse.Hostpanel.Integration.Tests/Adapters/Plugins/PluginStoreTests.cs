using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginStore"/> against Postgres: values are separate per plugin, per client reference and plugin wide,
/// a key is one row however often and however concurrently it is written, keys and values outside the rules are refused
/// without a write or a leak, a scope holds exactly its quota of keys and characters however many contexts write it at
/// once (counted per plugin and client reference), its totals always equal its rows, and a write saves through the
/// request's context so another context sees it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginStoreTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A small key cap for the tests that fill a scope, so a full scope costs a few writes and not a thousand.</summary>
    private const int SmallCap = 3;

    /// <summary>A small size cap, in characters, for the tests that fill a scope by size; well above one short key.</summary>
    private const long SmallChars = 50;

    /// <summary>How many writers race in the test that writes new keys into a nearly full scope at once.</summary>
    private const int Racers = 4;

    /// <summary>How many times a two-writer race is repeated, so the writes really overlap at least once.</summary>
    private const int RaceRounds = 8;

    /// <summary>A quota of <see cref="SmallCap"/> keys and the default size, for the tests about the key cap.</summary>
    private static readonly PluginStoreQuota KeyCapQuota = new(SmallCap, PluginApiOptions.DefaultMaxStoreCharsPerScope);

    /// <summary>A quota of the default keys and <see cref="SmallChars"/> characters, for the tests about the size cap.</summary>
    private static readonly PluginStoreQuota SizeCapQuota = new(PluginApiOptions.DefaultMaxStoreKeysPerScope, SmallChars);

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
    /// <param name="quota">What the scope may hold; the host's defaults unless a test sets a small quota.</param>
    /// <param name="signal">The call's quota signal; a fresh one unless the test reads it.</param>
    /// <returns>The store.</returns>
    private static PluginStore StoreOf(
        AppDbContext db,
        string pluginId,
        string? clientRef,
        PluginStoreQuota? quota = null,
        PluginStoreQuotaSignal? signal = null)
    {
        return new PluginStore(
            pluginId, clientRef, db, quota ?? new PluginApiOptions().StoreQuota, signal ?? new PluginStoreQuotaSignal());
    }

    /// <summary>Writes <paramref name="count"/> distinct keys, <c>key-0</c> upwards, through a store.</summary>
    /// <param name="store">The store to fill.</param>
    /// <param name="count">How many keys to write.</param>
    /// <returns>A task that completes when every key is written.</returns>
    private static async Task FillAsync(PluginStore store, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await store.SetAsync($"key-{i}", $"value-{i}", CancellationToken.None);
        }
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

    /// <summary>Reads the totals row of one scope straight from the table.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientRef">The client reference, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <returns>The totals, or <see langword="null"/> when the scope has never been written.</returns>
    private async Task<PluginDataScope?> ScopeAsync(string pluginId, string? clientRef)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataScopes.AsNoTracking()
            .SingleOrDefaultAsync(row => row.PluginId == pluginId && row.ClientRef == clientRef);
    }

    /// <summary>Asserts that a scope's totals equal the rows it holds: the key count and the characters of every key and value.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientRef">The client reference, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private async Task AssertTotalsMatchRowsAsync(string pluginId, string? clientRef)
    {
        var rows = (await RowsAsync(pluginId)).Where(row => row.ClientRef == clientRef).ToList();
        var totals = await ScopeAsync(pluginId, clientRef);

        totals.Should().NotBeNull();
        totals!.KeyCount.Should().Be(rows.Count);
        totals.StoredChars.Should().Be(rows.Sum(row => row.StoredChars));
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
    /// Two first writes of one key from two contexts at once both succeed and leave one row holding one of the two values,
    /// with totals of one key and that value's size: the loser of the race is retried and writes over the winner.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoConcurrentFirstWritesOfOneKey_BothSucceedAndLeaveOneRowWithExactTotals()
    {
        for (var round = 0; round < RaceRounds; round++)
        {
            var (plugin, clientRef) = (NewPlugin(), NewRef());
            string[] values = ["short", "a-much-longer-value"];

            await Task.WhenAll(values.Select(value => WithFreshContextAsync(
                db => StoreOf(db, plugin, clientRef).SetAsync("race", value, CancellationToken.None))));

            var row = (await RowsAsync(plugin)).Should().ContainSingle().Subject;
            values.Should().Contain(row.Value);
            await AssertTotalsMatchRowsAsync(plugin, clientRef);
        }
    }

    /// <summary>
    /// Several new keys written at once from separate contexts into a scope one key short of its cap: exactly one is
    /// admitted, every other write is refused with PLUGIN_STORE_QUOTA_EXCEEDED, and the scope never holds more than the
    /// cap.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ConcurrentNewKeysAtTheKeyCap_AdmitExactlyOne()
    {
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var quota = new PluginStoreQuota(SmallCap, PluginApiOptions.DefaultMaxStoreCharsPerScope);
        await WithFreshContextAsync(db => FillAsync(StoreOf(db, plugin, clientRef, quota), SmallCap - 1));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Racers).Select(i => WithFreshContextAsync(async db =>
        {
            var signal = new PluginStoreQuotaSignal();
            try
            {
                await StoreOf(db, plugin, clientRef, quota, signal).SetAsync($"racer-{i}", "v", CancellationToken.None);
                return (Admitted: true, Code: (string?)null, signal.Raised);
            }
            catch (DomainRuleException ex)
            {
                return (Admitted: false, Code: (string?)ex.Code, signal.Raised);
            }
        })));

        outcomes.Count(o => o.Admitted).Should().Be(1);
        outcomes.Where(o => !o.Admitted).Should().AllSatisfy(o =>
        {
            o.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
            o.Raised.Should().BeTrue();
        });
        (await RowsAsync(plugin)).Should().HaveCount(SmallCap);
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
    }

    /// <summary>
    /// A key whose scope row is missing, which is what a reader sees when a first writer commits between the scope read and
    /// the entry read, makes a set and a remove raise the retryable <see cref="ConcurrencyConflictException"/> and never
    /// the domain guard's <see cref="ArgumentOutOfRangeException"/>, and the attempt leaves nothing behind.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnEntryWithoutItsScopeRow_SetAndRemoveRaiseAConflictToRetry()
    {
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        await WithFreshContextAsync(async db =>
        {
            db.PluginDataEntries.Add(PluginDataEntry.Create(plugin, clientRef, "k", "a-longer-value"));
            await db.SaveChangesAsync();
        });

        await WithFreshContextAsync(async db =>
        {
            var store = StoreOf(db, plugin, clientRef);
            await FluentActions.Awaiting(() => store.SetAsync("k", "w", CancellationToken.None))
                .Should().ThrowAsync<ConcurrencyConflictException>();
            await FluentActions.Awaiting(() => store.RemoveAsync("k", CancellationToken.None))
                .Should().ThrowAsync<ConcurrencyConflictException>();
        });

        (await ScopeAsync(plugin, clientRef)).Should().BeNull();
        (await RowsAsync(plugin)).Should().ContainSingle().Which.Value.Should().Be("a-longer-value");
    }

    /// <summary>
    /// Two removals of one key from two contexts at once: one answers true and the other false, neither throws, and the
    /// scope's totals are back to empty.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoConcurrentRemovesOfOneKey_OneRemovesAndTheOtherFindsItGone()
    {
        for (var round = 0; round < RaceRounds; round++)
        {
            var (plugin, clientRef) = (NewPlugin(), NewRef());
            await WithFreshContextAsync(db => StoreOf(db, plugin, clientRef).SetAsync("k", "v", CancellationToken.None));

            var removed = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => WithFreshContextAsync(
                db => StoreOf(db, plugin, clientRef).RemoveAsync("k", CancellationToken.None))));

            removed.Should().BeEquivalentTo(new[] { true, false });
            (await RowsAsync(plugin)).Should().BeEmpty();
            var totals = await ScopeAsync(plugin, clientRef);
            totals!.KeyCount.Should().Be(0);
            totals.StoredChars.Should().Be(0);
        }
    }

    /// <summary>A write through one context after another context removed the key creates it again, with exact totals.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ASetAfterAnotherContextRemovedTheKey_CreatesItAgain()
    {
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        using var writing = factory.Services.CreateScope();
        var store = StoreOf(writing.ServiceProvider.GetRequiredService<AppDbContext>(), plugin, clientRef);
        await store.SetAsync("k", "first", CancellationToken.None);

        (await WithFreshContextAsync(db => StoreOf(db, plugin, clientRef).RemoveAsync("k", CancellationToken.None)))
            .Should().BeTrue();
        await store.SetAsync("k", "second", CancellationToken.None);

        (await RowsAsync(plugin)).Should().ContainSingle().Which.Value.Should().Be("second");
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
    }

    /// <summary>
    /// A write after another context changed the same key replaces the value and keeps the totals exact, though the
    /// first context had read the scope before.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ASetAfterAnotherContextChangedTheKey_KeepsTheTotalsExact()
    {
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        using var writing = factory.Services.CreateScope();
        var store = StoreOf(writing.ServiceProvider.GetRequiredService<AppDbContext>(), plugin, clientRef);
        await store.SetAsync("k", "first", CancellationToken.None);

        await WithFreshContextAsync(db => StoreOf(db, plugin, clientRef).SetAsync("k", "a-longer-value", CancellationToken.None));
        await store.SetAsync("k", "x", CancellationToken.None);

        (await RowsAsync(plugin)).Should().ContainSingle().Which.Value.Should().Be("x");
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
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

    /// <summary>A scope filled to exactly the cap accepts every write and holds that many rows.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FillingAScopeToTheCap_Succeeds()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = NewPlugin();
        var store = StoreOf(db, plugin, NewRef(), KeyCapQuota);

        await FillAsync(store, SmallCap);

        (await RowsAsync(plugin)).Should().HaveCount(SmallCap);
        (await store.GetAsync($"key-{SmallCap - 1}", CancellationToken.None)).Should().Be($"value-{SmallCap - 1}");
    }

    /// <summary>A new key past the key cap is refused with PLUGIN_STORE_QUOTA_EXCEEDED, raises the signal and writes nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ANewKeyPastTheCap_IsRefusedAndWritesNothing()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var signal = new PluginStoreQuotaSignal();
        var store = StoreOf(db, plugin, clientRef, KeyCapQuota, signal);
        await FillAsync(store, SmallCap);
        signal.Raised.Should().BeFalse();

        var refused = (await FluentActions.Awaiting(() => store.SetAsync("one-too-many", "v", CancellationToken.None))
            .Should().ThrowAsync<DomainRuleException>()).Which;

        refused.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
        signal.Raised.Should().BeTrue();
        (await RowsAsync(plugin)).Should().HaveCount(SmallCap).And.NotContain(row => row.Key == "one-too-many");
        (await store.GetAsync("one-too-many", CancellationToken.None)).Should().BeNull();
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
    }

    /// <summary>Replacing an existing key in a full scope succeeds and changes the value without adding a row.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReplacingAKeyInAFullScope_Succeeds()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = NewPlugin();
        var store = StoreOf(db, plugin, NewRef(), KeyCapQuota);
        await FillAsync(store, SmallCap);

        await store.SetAsync("key-0", "replaced", CancellationToken.None);

        (await store.GetAsync("key-0", CancellationToken.None)).Should().Be("replaced");
        (await RowsAsync(plugin)).Should().HaveCount(SmallCap);
    }

    /// <summary>A removed key frees room, so a full scope accepts a new key after one is removed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RemovingAKeyFromAFullScope_FreesRoomForANewKey()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var store = StoreOf(db, plugin, clientRef, KeyCapQuota);
        await FillAsync(store, SmallCap);

        (await store.RemoveAsync("key-0", CancellationToken.None)).Should().BeTrue();
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
        await store.SetAsync("fresh", "v", CancellationToken.None);

        (await store.GetAsync("fresh", CancellationToken.None)).Should().Be("v");
        (await RowsAsync(plugin)).Should().HaveCount(SmallCap);
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
    }

    /// <summary>The plugin-wide scope and a client scope of one plugin are counted separately: filling one leaves the other open.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PluginWideAndClientScopes_AreCountedSeparately()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var wide = StoreOf(db, plugin, null, KeyCapQuota);
        var perClient = StoreOf(db, plugin, clientRef, KeyCapQuota);

        await FillAsync(wide, SmallCap);
        await FluentActions.Awaiting(() => wide.SetAsync("extra", "v", CancellationToken.None)).Should().ThrowAsync<DomainRuleException>();
        await perClient.SetAsync("extra", "of-client", CancellationToken.None);
        await FillAsync(perClient, SmallCap - 1);

        (await perClient.GetAsync("extra", CancellationToken.None)).Should().Be("of-client");
        await FluentActions.Awaiting(() => perClient.SetAsync("another", "v", CancellationToken.None)).Should().ThrowAsync<DomainRuleException>();
        (await wide.GetAsync("extra", CancellationToken.None)).Should().BeNull();
        (await RowsAsync(plugin)).Should().HaveCount(SmallCap * 2);
    }

    /// <summary>Two client references of one plugin are counted separately: filling one leaves the other open.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoClientScopes_AreCountedSeparately()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, x, y) = (NewPlugin(), NewRef(), NewRef());
        var storeX = StoreOf(db, plugin, x, KeyCapQuota);
        var storeY = StoreOf(db, plugin, y, KeyCapQuota);

        await FillAsync(storeX, SmallCap);
        var refused = (await FluentActions.Awaiting(() => storeX.SetAsync("extra", "v", CancellationToken.None))
            .Should().ThrowAsync<DomainRuleException>()).Which;
        await storeY.SetAsync("extra", "of-y", CancellationToken.None);

        refused.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
        (await storeY.GetAsync("extra", CancellationToken.None)).Should().Be("of-y");
        (await storeX.GetAsync("extra", CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// A longer value that would take the scope one character past its size cap is refused with
    /// PLUGIN_STORE_QUOTA_EXCEEDED and raises the signal; the old value and the totals are unchanged.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AValueOverTheSizeCap_IsRefusedAndKeepsTheOldValueAndTotals()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var signal = new PluginStoreQuotaSignal();
        var store = StoreOf(db, plugin, clientRef, SizeCapQuota, signal);
        var fits = new string('x', (int)SmallChars - 1);
        await store.SetAsync("k", fits, CancellationToken.None);
        var before = await ScopeAsync(plugin, clientRef);

        var refused = (await FluentActions.Awaiting(() => store.SetAsync("k", fits + "xx", CancellationToken.None))
            .Should().ThrowAsync<DomainRuleException>()).Which;

        refused.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
        signal.Raised.Should().BeTrue();
        (await store.GetAsync("k", CancellationToken.None)).Should().Be(fits);
        var after = await ScopeAsync(plugin, clientRef);
        after!.KeyCount.Should().Be(before!.KeyCount);
        after.StoredChars.Should().Be(before.StoredChars).And.Be(SmallChars);
        after.Revision.Should().Be(before.Revision);
    }

    /// <summary>A new key whose size would pass the size cap is refused, though the key cap has room.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ANewKeyOverTheSizeCap_IsRefusedAndWritesNothing()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var signal = new PluginStoreQuotaSignal();
        var store = StoreOf(db, plugin, clientRef, SizeCapQuota, signal);
        await store.SetAsync("a", new string('x', (int)SmallChars - 2), CancellationToken.None);

        var refused = (await FluentActions.Awaiting(() => store.SetAsync("b", "y", CancellationToken.None))
            .Should().ThrowAsync<DomainRuleException>()).Which;

        refused.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
        signal.Raised.Should().BeTrue();
        (await RowsAsync(plugin)).Should().ContainSingle().Which.Key.Should().Be("a");
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
    }

    /// <summary>
    /// Values that grow up to the size cap exactly and shrink again are admitted, the totals following every write, and
    /// the signal stays down.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpdatesWithinTheSizeCap_AreAdmittedAndTracked()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var signal = new PluginStoreQuotaSignal();
        var store = StoreOf(db, plugin, clientRef, SizeCapQuota, signal);

        await store.SetAsync("k", "short", CancellationToken.None);
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
        await store.SetAsync("k", new string('x', (int)SmallChars - 1), CancellationToken.None);
        (await ScopeAsync(plugin, clientRef))!.StoredChars.Should().Be(SmallChars);
        await store.SetAsync("k", string.Empty, CancellationToken.None);

        signal.Raised.Should().BeFalse();
        (await store.GetAsync("k", CancellationToken.None)).Should().BeEmpty();
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
    }

    /// <summary>Removing a key frees its characters, so a value that did not fit before fits after.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RemovingAKey_FreesItsCharacters()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (plugin, clientRef) = (NewPlugin(), NewRef());
        var store = StoreOf(db, plugin, clientRef, SizeCapQuota);
        var large = new string('x', (int)SmallChars - 1);
        await store.SetAsync("a", large, CancellationToken.None);

        (await store.RemoveAsync("a", CancellationToken.None)).Should().BeTrue();
        await store.SetAsync("b", large, CancellationToken.None);

        (await store.GetAsync("b", CancellationToken.None)).Should().Be(large);
        await AssertTotalsMatchRowsAsync(plugin, clientRef);
    }

    /// <summary>Two plugins of one client have their own totals: filling one plugin's scope leaves the other's open.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoPluginScopes_AreCountedSeparately()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (a, b, clientRef) = (NewPlugin(), NewPlugin(), NewRef());
        var storeA = StoreOf(db, a, clientRef, KeyCapQuota);
        var storeB = StoreOf(db, b, clientRef, KeyCapQuota);

        await FillAsync(storeA, SmallCap);
        await FluentActions.Awaiting(() => storeA.SetAsync("extra", "v", CancellationToken.None))
            .Should().ThrowAsync<DomainRuleException>();
        await FillAsync(storeB, SmallCap);

        (await RowsAsync(b)).Should().HaveCount(SmallCap);
        await AssertTotalsMatchRowsAsync(a, clientRef);
        await AssertTotalsMatchRowsAsync(b, clientRef);
    }

    /// <summary>Runs <paramref name="work"/> on a context of its own, as a concurrent call would.</summary>
    /// <param name="work">What to do with the context.</param>
    /// <returns>A task that completes when the work has.</returns>
    private async Task WithFreshContextAsync(Func<AppDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Runs <paramref name="work"/> on a context of its own, as a concurrent call would, and answers its result.</summary>
    /// <typeparam name="T">What the work answers.</typeparam>
    /// <param name="work">What to do with the context.</param>
    /// <returns>What the work answered.</returns>
    private async Task<T> WithFreshContextAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
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
