using Hostpanel.Sdk.Plugins.Store;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Time;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The advanced plugin store (<see cref="IPluginStoreAdvanced"/>) of <see cref="PluginStore"/> and
/// <see cref="PluginWideStore"/> against Postgres: the SDK's own contract checks, values that expire on a test clock, the
/// key limit that ignores expired keys, the atomic set-if-absent under concurrency, the listing by prefix with the SDK's
/// limits, and the per plugin and per client scope of all of it.
/// </summary>
public sealed class PluginAdvancedStoreTests : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>A small key cap for the tests that fill a scope, so a full scope costs a few writes and not a thousand.</summary>
    private const int SmallCap = 3;

    /// <summary>How many callers race for one key in the concurrency test.</summary>
    private const int Racers = 8;

    /// <summary>A quota of <see cref="SmallCap"/> keys and the default size.</summary>
    private static readonly PluginStoreQuota KeyCapQuota = new(SmallCap, PluginApiOptions.DefaultMaxStoreCharsPerScope);

    /// <summary>A time to live that is neither near zero nor near the limit.</summary>
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>A time past <see cref="Minute"/>.</summary>
    private static readonly TimeSpan TwoMinutes = TimeSpan.FromMinutes(2);

    /// <summary>The shared API + PostgreSQL host.</summary>
    private readonly IntegrationTestFactory _factory;

    /// <summary>The test clock every store of this class reads; moved by the tests, never by the wall.</summary>
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    /// <summary>The host whose clock is <see cref="_time"/>, built when a test needs a store that opens its own scopes.</summary>
    private readonly Lazy<WebApplicationFactory<Program>> _clockedHost;

    /// <summary>Initializes the tests.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    public PluginAdvancedStoreTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _clockedHost = new Lazy<WebApplicationFactory<Program>>(() => factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new SystemClock(_time));
            })));
    }

    /// <summary>Disposes the clocked host when a test built one.</summary>
    public void Dispose()
    {
        if (_clockedHost.IsValueCreated)
        {
            _clockedHost.Value.Dispose();
        }
    }

    /// <summary>A plugin id that no other test uses.</summary>
    /// <returns>The id.</returns>
    private static string NewPlugin() => $"adv-{Guid.NewGuid():N}";

    /// <summary>A client reference that no other test uses, in the shape of the real ones.</summary>
    /// <returns>The reference.</returns>
    private static string NewRef() => Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray())
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>Builds a store over a context, reading the test clock.</summary>
    /// <param name="db">The context.</param>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientRef">The client reference, or <see langword="null"/> for plugin wide.</param>
    /// <param name="quota">What the scope may hold; the host's defaults unless a test sets a small quota.</param>
    /// <returns>The store.</returns>
    private PluginStore StoreOf(AppDbContext db, string pluginId, string? clientRef = null, PluginStoreQuota? quota = null)
    {
        return new PluginStore(
            pluginId, clientRef, db, quota ?? new PluginApiOptions().StoreQuota, new PluginStoreQuotaSignal(), new SystemClock(_time));
    }

    /// <summary>Builds a plugin-wide store that opens a scope and a context per operation, reading the test clock.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The store.</returns>
    private PluginWideStore WideStoreOf(string pluginId)
    {
        return new PluginWideStore(pluginId, _clockedHost.Value.Services.GetRequiredService<IServiceScopeFactory>());
    }

    /// <summary>Lets time pass on the test clock; the <c>advance</c> argument of the SDK's checks.</summary>
    /// <param name="span">How long.</param>
    /// <returns>A completed task.</returns>
    private Task AdvanceAsync(TimeSpan span)
    {
        _time.Advance(span);
        return Task.CompletedTask;
    }

    /// <summary>Reads the rows of a plugin straight from the table.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The rows.</returns>
    private async Task<List<PluginDataEntry>> RowsAsync(string pluginId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataEntries.AsNoTracking().Where(row => row.PluginId == pluginId).ToListAsync();
    }

    /// <summary>Asserts that a plugin-wide scope's totals equal the rows it holds.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private async Task AssertTotalsMatchRowsAsync(string pluginId)
    {
        var rows = await RowsAsync(pluginId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var totals = await db.PluginDataScopes.AsNoTracking()
            .SingleAsync(row => row.PluginId == pluginId && row.ClientRef == null);

        totals.KeyCount.Should().Be(rows.Count);
        totals.StoredChars.Should().Be(rows.Sum(row => row.StoredChars));
    }

    /// <summary>The SDK's check that set-if-absent is atomic holds for the plugin-wide store, with the callers in separate scopes.</summary>
    /// <returns>A task that completes when the check passed.</returns>
    [Fact]
    public async Task Contract_SetIfAbsentIsAtomicAsync()
    {
        await PluginStoreContract.AssertSetIfAbsentIsAtomicAsync(WideStoreOf(NewPlugin()), Racers, CancellationToken.None);
    }

    /// <summary>The SDK's check that an expired key reads as absent holds, with the test clock as the passing time.</summary>
    /// <returns>A task that completes when the check passed.</returns>
    [Fact]
    public async Task Contract_ExpiredKeyReadsAsAbsentAsync()
    {
        await PluginStoreContract.AssertExpiredKeyReadsAsAbsentAsync(WideStoreOf(NewPlugin()), AdvanceAsync, CancellationToken.None);
    }

    /// <summary>The SDK's check that a plain write clears the time to live holds.</summary>
    /// <returns>A task that completes when the check passed.</returns>
    [Fact]
    public async Task Contract_PlainSetClearsTimeToLiveAsync()
    {
        await PluginStoreContract.AssertPlainSetClearsTimeToLiveAsync(WideStoreOf(NewPlugin()), AdvanceAsync, CancellationToken.None);
    }

    /// <summary>The SDK's check of the time to live bounds holds.</summary>
    /// <returns>A task that completes when the check passed.</returns>
    [Fact]
    public async Task Contract_TimeToLiveBoundsAsync()
    {
        await PluginStoreContract.AssertTimeToLiveBoundsAsync(WideStoreOf(NewPlugin()), CancellationToken.None);
    }

    /// <summary>The SDK's check of the listing by prefix and limit holds.</summary>
    /// <returns>A task that completes when the check passed.</returns>
    [Fact]
    public async Task Contract_ListKeysHonoursPrefixAndLimitAsync()
    {
        await PluginStoreContract.AssertListKeysHonoursPrefixAndLimitAsync(WideStoreOf(NewPlugin()), CancellationToken.None);
    }

    /// <summary>
    /// Several callers racing to set an expired key if absent: exactly one of them writes, the value is the winner's and
    /// the key is one row.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SetIfAbsent_OnAnExpiredKey_UnderConcurrency_HasOneWinnerAsync()
    {
        var plugin = NewPlugin();
        var store = WideStoreOf(plugin);
        await store.SetAsync("marker", "old", Minute, CancellationToken.None);
        _time.Advance(TwoMinutes);

        var races = Enumerable.Range(0, Racers)
            .Select(caller => Task.Run(() => store.SetIfAbsentAsync("marker", $"caller-{caller}", Minute, CancellationToken.None)))
            .ToArray();
        var results = await Task.WhenAll(races);

        results.Count(won => won).Should().Be(1);
        var winner = Array.IndexOf(results, true);
        (await store.GetAsync("marker", CancellationToken.None)).Should().Be($"caller-{winner}");
        (await RowsAsync(plugin)).Should().ContainSingle();
        await AssertTotalsMatchRowsAsync(plugin);
    }

    /// <summary>A value written with a time to live is read until it expires and absent from then on.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SetWithTimeToLive_IsReadUntilItExpiresAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), NewPlugin());
        await store.SetAsync("k", "v", Minute, CancellationToken.None);

        _time.Advance(Minute - TimeSpan.FromSeconds(1));
        (await store.GetAsync("k", CancellationToken.None)).Should().Be("v");

        _time.Advance(TimeSpan.FromSeconds(1));
        (await store.GetAsync("k", CancellationToken.None)).Should().BeNull();
    }

    /// <summary>Expired keys do not count toward the key limit: a new key fits where only expired ones fill the scope.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ExpiredKeys_DoNotCountTowardTheKeyLimitAsync()
    {
        var plugin = NewPlugin();
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), plugin, quota: KeyCapQuota);
        for (var i = 0; i < SmallCap; i++)
        {
            await store.SetAsync($"old-{i}", "v", Minute, CancellationToken.None);
        }

        _time.Advance(TwoMinutes);
        await store.SetAsync("fresh", "v", CancellationToken.None);

        (await store.ListKeysAsync(string.Empty, PluginStoreLimits.MaxListLimit, CancellationToken.None)).Should().Equal("fresh");
        (await RowsAsync(plugin)).Select(row => row.Key).Should().Equal("fresh");
        await AssertTotalsMatchRowsAsync(plugin);
    }

    /// <summary>Keys that have not expired still fill the scope: the write of a new key is refused.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task LiveKeysWithTimeToLive_StillCountTowardTheKeyLimitAsync()
    {
        var plugin = NewPlugin();
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), plugin, quota: KeyCapQuota);
        for (var i = 0; i < SmallCap; i++)
        {
            await store.SetAsync($"live-{i}", "v", Minute, CancellationToken.None);
        }

        var write = () => store.SetAsync("one-too-many", "v", Minute, CancellationToken.None);
        var conditional = () => store.SetIfAbsentAsync("one-too-many", "v", null, CancellationToken.None);

        await write.Should().ThrowAsync<PluginStoreQuotaException>();
        await conditional.Should().ThrowAsync<PluginStoreQuotaException>();
        (await RowsAsync(plugin)).Should().HaveCount(SmallCap);
        await AssertTotalsMatchRowsAsync(plugin);
    }

    /// <summary>
    /// The plugin-wide store a plugin takes in its constructor refuses a new key of a full scope with the SDK's
    /// <see cref="PluginStoreQuotaException"/>, the type plugins catch, for the plain, the expiring and the conditional write.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheWideStore_RefusesANewKeyOfAFullScope_WithTheSdkQuotaExceptionAsync()
    {
        using var host = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.Configure<PluginApiOptions>(options => options.MaxStoreKeysPerScope = 1)));
        var store = new PluginWideStore(NewPlugin(), host.Services.GetRequiredService<IServiceScopeFactory>());
        await store.SetAsync("first", "v", CancellationToken.None);

        await FluentActions.Awaiting(() => store.SetAsync("second", "v", CancellationToken.None)).Should().ThrowAsync<PluginStoreQuotaException>();
        await FluentActions.Awaiting(() => store.SetAsync("second", "v", Minute, CancellationToken.None)).Should().ThrowAsync<PluginStoreQuotaException>();
        await FluentActions.Awaiting(() => store.SetIfAbsentAsync("second", "v", null, CancellationToken.None)).Should().ThrowAsync<PluginStoreQuotaException>();
        (await store.SetIfAbsentAsync("first", "other", null, CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>A key that exists answers false to a set-if-absent on a full scope and never throws the quota refusal.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SetIfAbsent_OnAnExistingKeyOfAFullScope_AnswersFalseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), NewPlugin(), quota: KeyCapQuota);
        for (var i = 0; i < SmallCap; i++)
        {
            await store.SetAsync($"k-{i}", "v", CancellationToken.None);
        }

        (await store.SetIfAbsentAsync("k-1", "other", null, CancellationToken.None)).Should().BeFalse();
        (await store.GetAsync("k-1", CancellationToken.None)).Should().Be("v");
    }

    /// <summary>A set-if-absent on a live key changes neither its value nor its expiry.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SetIfAbsent_OnALiveKey_ChangesNothingAsync()
    {
        var plugin = NewPlugin();
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), plugin);
        await store.SetAsync("k", "first", Minute, CancellationToken.None);
        var before = (await RowsAsync(plugin)).Single().ExpiresAt;

        var written = await store.SetIfAbsentAsync("k", "second", TimeSpan.FromDays(1), CancellationToken.None);

        written.Should().BeFalse();
        (await store.GetAsync("k", CancellationToken.None)).Should().Be("first");
        (await RowsAsync(plugin)).Single().ExpiresAt.Should().Be(before);
    }

    /// <summary>A set-if-absent on an expired key counts it as absent: it writes the new value and the new expiry on the same row.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SetIfAbsent_OnAnExpiredKey_WritesItAgainAsync()
    {
        var plugin = NewPlugin();
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), plugin);
        await store.SetAsync("k", "old", Minute, CancellationToken.None);
        _time.Advance(TwoMinutes);

        var written = await store.SetIfAbsentAsync("k", "new", null, CancellationToken.None);

        written.Should().BeTrue();
        (await store.GetAsync("k", CancellationToken.None)).Should().Be("new");
        var row = (await RowsAsync(plugin)).Single();
        row.ExpiresAt.Should().BeNull();
        await AssertTotalsMatchRowsAsync(plugin);
    }

    /// <summary>A removal of an expired key answers false, as for an absent key, and still frees the row.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Remove_OfAnExpiredKey_AnswersFalseAndFreesTheRowAsync()
    {
        var plugin = NewPlugin();
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), plugin);
        await store.SetAsync("k", "v", Minute, CancellationToken.None);
        _time.Advance(TwoMinutes);

        var removed = await store.RemoveAsync("k", CancellationToken.None);

        removed.Should().BeFalse();
        (await RowsAsync(plugin)).Should().BeEmpty();
        await AssertTotalsMatchRowsAsync(plugin);
    }

    /// <summary>A time to live that is zero, negative or past the limit is refused without a write, whichever write is asked.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ATimeToLiveOutsideTheLimits_IsRefusedWithoutAWriteAsync()
    {
        var plugin = NewPlugin();
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), plugin);
        TimeSpan[] refused = [TimeSpan.Zero, -Minute, PluginStoreLimits.MaxTimeToLive + TimeSpan.FromSeconds(1)];

        foreach (var timeToLive in refused)
        {
            var set = () => store.SetAsync("k", "v", timeToLive, CancellationToken.None);
            var conditional = () => store.SetIfAbsentAsync("k", "v", timeToLive, CancellationToken.None);

            await set.Should().ThrowAsync<ArgumentOutOfRangeException>();
            await conditional.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        (await RowsAsync(plugin)).Should().BeEmpty();
    }

    /// <summary>The longest time to live the SDK allows is accepted.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheLongestTimeToLive_IsAcceptedAsync()
    {
        var plugin = NewPlugin();
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), plugin);

        await store.SetAsync("k", "v", PluginStoreLimits.MaxTimeToLive, CancellationToken.None);

        _time.Advance(PluginStoreLimits.MaxTimeToLive - TimeSpan.FromSeconds(1));
        (await store.GetAsync("k", CancellationToken.None)).Should().Be("v");
    }

    /// <summary>A listing returns at most the limit asked for, and refuses a limit below 1 or above the SDK's maximum.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListKeys_HonoursTheLimitsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), NewPlugin());
        foreach (var key in new[] { "a", "b", "c", "d" })
        {
            await store.SetAsync(key, "v", CancellationToken.None);
        }

        (await store.ListKeysAsync(string.Empty, 2, CancellationToken.None)).Should().Equal("a", "b");
        (await store.ListKeysAsync(string.Empty, PluginStoreLimits.MaxListLimit, CancellationToken.None)).Should().HaveCount(4);
        foreach (var limit in new[] { 0, -1, PluginStoreLimits.MaxListLimit + 1 })
        {
            var list = () => store.ListKeysAsync(string.Empty, limit, CancellationToken.None);

            await list.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
    }

    /// <summary>
    /// A listing is case sensitive and ordinal ("B" sorts before "a"), takes the prefix literally (an underscore is not a
    /// wildcard) and never lists an expired key.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListKeys_IsOrdinalLiteralAndLeavesOutExpiredKeysAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), NewPlugin());
        foreach (var key in new[] { "a", "B", "A", "a_b", "axb" })
        {
            await store.SetAsync(key, "v", CancellationToken.None);
        }

        await store.SetAsync("gone", "v", Minute, CancellationToken.None);

        _time.Advance(TwoMinutes);

        (await store.ListKeysAsync(string.Empty, PluginStoreLimits.MaxListLimit, CancellationToken.None))
            .Should().Equal("A", "B", "a", "a_b", "axb");
        (await store.ListKeysAsync("a_", PluginStoreLimits.MaxListLimit, CancellationToken.None)).Should().Equal("a_b");
        (await store.ListKeysAsync("b", PluginStoreLimits.MaxListLimit, CancellationToken.None)).Should().BeEmpty();
    }

    /// <summary>A prefix outside the key rules is refused like a bad key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListKeys_WithABadPrefix_IsRefusedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var store = StoreOf(scope.ServiceProvider.GetRequiredService<AppDbContext>(), NewPlugin());

        foreach (var prefix in new[] { "a b", "a/", new string('a', PluginDataEntry.MaxKeyLength + 1) })
        {
            var list = () => store.ListKeysAsync(prefix, 10, CancellationToken.None);

            (await list.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be(PluginStoreErrors.KeyInvalid);
        }
    }

    /// <summary>Every advanced operation stays in the plugin and the client reference of its store.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheAdvancedOperations_KeepTheScopeOfTheStoreAsync()
    {
        var (a, b, clientRef) = (NewPlugin(), NewPlugin(), NewRef());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wide = StoreOf(db, a);
        var client = StoreOf(db, a, clientRef);
        var other = StoreOf(db, b);

        (await wide.SetIfAbsentAsync("k", "wide", Minute, CancellationToken.None)).Should().BeTrue();
        (await client.SetIfAbsentAsync("k", "client", Minute, CancellationToken.None)).Should().BeTrue();
        (await other.SetIfAbsentAsync("k", "other", Minute, CancellationToken.None)).Should().BeTrue();
        await wide.SetAsync("only-wide", "v", CancellationToken.None);

        (await wide.ListKeysAsync(string.Empty, 10, CancellationToken.None)).Should().Equal("k", "only-wide");
        (await client.ListKeysAsync(string.Empty, 10, CancellationToken.None)).Should().Equal("k");
        (await other.ListKeysAsync(string.Empty, 10, CancellationToken.None)).Should().Equal("k");
        (await client.GetAsync("k", CancellationToken.None)).Should().Be("client");
    }
}
