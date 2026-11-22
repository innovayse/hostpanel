using Hostpanel.Sdk.Plugins.Store;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SweepExpiredPluginDataCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Time;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>SweepExpiredPluginDataCronCommand</c>, the recurring job that removes the plugin store values whose time to live has
/// passed: a pass removes the expired rows of every scope and keeps each scope's totals equal to the rows left, leaves live
/// values and values without an expiry alone, and a pass with nothing expired changes nothing.
/// </summary>
public sealed class SweepExpiredPluginDataCronTests : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>The time to live of the values that are about to expire.</summary>
    private static readonly TimeSpan Short = TimeSpan.FromMinutes(1);

    /// <summary>The time to live of the values that outlast the test.</summary>
    private static readonly TimeSpan Long = TimeSpan.FromDays(1);

    /// <summary>The time the test clock is moved by; past <see cref="Short"/>, short of <see cref="Long"/>.</summary>
    private static readonly TimeSpan Wait = TimeSpan.FromMinutes(5);

    /// <summary>The test clock the host's <see cref="IClock"/> reads.</summary>
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    /// <summary>The host whose clock is <see cref="_time"/>.</summary>
    private readonly WebApplicationFactory<Program> _host;

    /// <summary>The plugin whose expired entries the data context refuses to delete, or <see langword="null"/> while no save is refused.</summary>
    private string? _failingPlugin;

    /// <summary>Initializes the tests.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    public SweepExpiredPluginDataCronTests(IntegrationTestFactory factory)
    {
        _host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new SystemClock(_time));
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new FailingSaveInterceptor(RefusesFailingPlugin)));
            }));
    }

    /// <summary>Tells whether a pending change set deletes an entry of <see cref="_failingPlugin"/>, the save the test makes fail.</summary>
    /// <param name="tracker">The change tracker of the save.</param>
    /// <returns><see langword="true"/> when the save must be refused.</returns>
    private bool RefusesFailingPlugin(ChangeTracker tracker)
    {
        var failing = _failingPlugin;
        return failing is not null
            && tracker.Entries<PluginDataEntry>().Any(entry => entry.State == EntityState.Deleted && entry.Entity.PluginId == failing);
    }

    /// <summary>Counts the entries of a plugin straight from the table.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>How many rows the plugin has.</returns>
    private async Task<int> CountEntriesAsync(string pluginId)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataEntries.AsNoTracking().CountAsync(row => row.PluginId == pluginId);
    }

    /// <summary>Disposes the host.</summary>
    public void Dispose()
    {
        _host.Dispose();
    }

    /// <summary>Builds a plugin-wide store of a plugin on the test host.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The store.</returns>
    private PluginWideStore StoreOf(string pluginId)
    {
        return new PluginWideStore(pluginId, _host.Services.GetRequiredService<IServiceScopeFactory>());
    }

    /// <summary>Runs one pass of the job on the host.</summary>
    /// <returns>A task that completes when the pass ended.</returns>
    private async Task RunPassAsync()
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new SweepExpiredPluginDataCronCommand());
    }

    /// <summary>A pass removes the expired rows and leaves the live ones and the ones that never expire, with the totals matching.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APass_RemovesExpiredRowsAndKeepsTheRestAsync()
    {
        var plugin = $"sweep-{Guid.NewGuid():N}";
        var store = StoreOf(plugin);
        await store.SetAsync("expiring-a", "value", Short, CancellationToken.None);
        await store.SetAsync("expiring-b", "value", Short, CancellationToken.None);
        await store.SetAsync("lasting", "value", Long, CancellationToken.None);
        await store.SetAsync("forever", "value", CancellationToken.None);
        _time.Advance(Wait);

        await RunPassAsync();

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.PluginDataEntries.AsNoTracking().Where(row => row.PluginId == plugin).ToListAsync();
        rows.Select(row => row.Key).Should().BeEquivalentTo("lasting", "forever");
        var totals = await db.PluginDataScopes.AsNoTracking().SingleAsync(row => row.PluginId == plugin && row.ClientRef == null);
        totals.KeyCount.Should().Be(2);
        totals.StoredChars.Should().Be(rows.Sum(row => row.StoredChars));
    }

    /// <summary>A pass sweeps every scope of a plugin, the plugin-wide one and the per client ones, and not another plugin's live rows.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APass_SweepsEveryScopeOfAPluginAsync()
    {
        var (plugin, other) = ($"sweep-{Guid.NewGuid():N}", $"sweep-{Guid.NewGuid():N}");
        var clientRef = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var client = new PluginStore(
                plugin, clientRef, db, new PluginApiOptions().StoreQuota,
                new PluginStoreQuotaSignal(), new SystemClock(_time));
            await client.SetAsync("expiring", "value", Short, CancellationToken.None);
        }

        await StoreOf(plugin).SetAsync("expiring", "value", Short, CancellationToken.None);
        await StoreOf(other).SetAsync("lasting", "value", Long, CancellationToken.None);
        _time.Advance(Wait);

        await RunPassAsync();

        using var check = _host.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.PluginDataEntries.AsNoTracking().Where(row => row.PluginId == plugin).CountAsync()).Should().Be(0);
        (await context.PluginDataEntries.AsNoTracking().Where(row => row.PluginId == other).CountAsync()).Should().Be(1);
        (await context.PluginDataScopes.AsNoTracking().Where(row => row.PluginId == plugin).ToListAsync())
            .Should().OnlyContain(row => row.KeyCount == 0 && row.StoredChars == 0);
    }

    /// <summary>A pass with nothing expired changes nothing, and a second pass after a sweep finds nothing to do.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APassWithNothingExpired_ChangesNothingAsync()
    {
        var plugin = $"sweep-{Guid.NewGuid():N}";
        var store = StoreOf(plugin);
        await store.SetAsync("lasting", "value", Long, CancellationToken.None);

        await RunPassAsync();
        await RunPassAsync();

        (await store.GetAsync("lasting", CancellationToken.None)).Should().Be("value");
        (await store.ListKeysAsync(string.Empty, PluginStoreLimits.MaxListLimit, CancellationToken.None)).Should().Equal("lasting");
    }

    /// <summary>
    /// A scope whose delete fails does not stop the pass: the other scopes are still swept, the failed one keeps its rows,
    /// and the next pass, once the failure is gone, sweeps it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APass_WhenOneScopeFails_StillSweepsTheOthersAndRetriesItNextTimeAsync()
    {
        var (failing, healthy) = ($"sweep-{Guid.NewGuid():N}", $"sweep-{Guid.NewGuid():N}");
        await StoreOf(failing).SetAsync("expiring", "value", Short, CancellationToken.None);
        await StoreOf(healthy).SetAsync("expiring", "value", Short, CancellationToken.None);
        _time.Advance(Wait);
        _failingPlugin = failing;

        try
        {
            await RunPassAsync();

            (await CountEntriesAsync(healthy)).Should().Be(0);
            (await CountEntriesAsync(failing)).Should().Be(1);
        }
        finally
        {
            _failingPlugin = null;
        }

        await RunPassAsync();

        (await CountEntriesAsync(failing)).Should().Be(0);
    }
}
