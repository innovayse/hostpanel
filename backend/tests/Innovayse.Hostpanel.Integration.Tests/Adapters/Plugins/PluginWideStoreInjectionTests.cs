using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The plugin-wide store that <see cref="PluginActivator"/> hands a plugin through its constructor: it persists across
/// activations, shares the plugin's rows with a call store that has no client, never sees a client's rows or another
/// plugin's, enforces the same size limit, and is removed by the plugin purge.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginWideStoreInjectionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client reference in the shape of the real ones, unique per call.</summary>
    /// <returns>The reference.</returns>
    private static string NewRef() => Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray())
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>Builds a call store of one plugin and client reference over a context, with the host's default quota.</summary>
    /// <param name="db">The context.</param>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientRef">The client reference, or <see langword="null"/> for the clientless call store.</param>
    /// <returns>The store.</returns>
    private static PluginStore CallStore(AppDbContext db, string pluginId, string? clientRef)
    {
        return new PluginStore(pluginId, clientRef, db, new PluginApiOptions().StoreQuota, new PluginStoreQuotaSignal());
    }

    /// <summary>Enables the plugin if it is not yet and activates a new instance of it over the given context.</summary>
    /// <param name="db">The context the activator reads and the injected store writes through.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The activated plugin.</returns>
    private async Task<StoreTakingPaymentPlugin> ActivateAsync(AppDbContext db, string pluginId)
    {
        var plugin = PluginActivationTestSeeds.Plugin(pluginId, PluginType.Payment, typeof(StoreTakingPaymentPlugin));
        var enabledKey = IntegrationSettingKeys.EnabledKey(pluginId);
        if (!await db.Settings.AnyAsync(s => s.Key == enabledKey))
        {
            await PluginActivationTestSeeds.EnableAsync(db, pluginId);
        }

        var activation = await PluginActivationTestSeeds.Activator(factory, db, factory.Services)
            .ActivateAsync<IPaymentPlugin>(plugin, CancellationToken.None);

        return (StoreTakingPaymentPlugin)activation.Instance!;
    }

    /// <summary>A value written through the injected store is read back by a new activation of the plugin, in another context.</summary>
    [Fact]
    public async Task InjectedStore_WrittenByOneActivation_IsReadByTheNext()
    {
        var pluginId = PluginActivationTestSeeds.NewId("wide");
        using (var first = factory.Services.CreateScope())
        {
            var plugin = await ActivateAsync(first.ServiceProvider.GetRequiredService<AppDbContext>(), pluginId);
            await plugin.Store.SetAsync("order:1", "gw-1", CancellationToken.None);
        }

        using var second = factory.Services.CreateScope();
        var again = await ActivateAsync(second.ServiceProvider.GetRequiredService<AppDbContext>(), pluginId);

        (await again.Store.GetAsync("order:1", CancellationToken.None)).Should().Be("gw-1");
    }

    /// <summary>The injected store is plugin wide: a client's row of the same plugin and key is never read, changed or removed by it.</summary>
    [Fact]
    public async Task InjectedStore_SameKeyAsClientRow_StaysSeparate()
    {
        var pluginId = PluginActivationTestSeeds.NewId("wide");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientStore = CallStore(db, pluginId, NewRef());
        await clientStore.SetAsync("k", "client", CancellationToken.None);
        var plugin = await ActivateAsync(db, pluginId);

        (await plugin.Store.GetAsync("k", CancellationToken.None)).Should().BeNull("a client's row is not plugin wide");
        await plugin.Store.SetAsync("k", "wide", CancellationToken.None);
        (await plugin.Store.RemoveAsync("k", CancellationToken.None)).Should().BeTrue();

        (await clientStore.GetAsync("k", CancellationToken.None)).Should().Be("client");
    }

    /// <summary>The injected store shares its rows with the clientless call store of its plugin, and never reaches another plugin's.</summary>
    [Fact]
    public async Task InjectedStore_SharesRowsWithClientlessCallStore_NotWithOtherPlugin()
    {
        var pluginId = PluginActivationTestSeeds.NewId("wide");
        var otherId = PluginActivationTestSeeds.NewId("wide");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await CallStore(db, pluginId, null).SetAsync("k", "mine", CancellationToken.None);
        var other = await ActivateAsync(db, otherId);
        var plugin = await ActivateAsync(db, pluginId);

        (await plugin.Store.GetAsync("k", CancellationToken.None)).Should().Be("mine");
        (await other.Store.GetAsync("k", CancellationToken.None)).Should().BeNull("another plugin never sees these rows");
    }

    /// <summary>The injected store refuses a value over the size limit, like the call store.</summary>
    [Fact]
    public async Task InjectedStore_ValueOverLimit_IsRefused()
    {
        var pluginId = PluginActivationTestSeeds.NewId("wide");
        using var scope = factory.Services.CreateScope();
        var plugin = await ActivateAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), pluginId);

        var act = () => plugin.Store.SetAsync("k", new string('x', PluginDataEntry.MaxValueLength + 1), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>();
    }

    /// <summary>
    /// A store write runs in its own unit of work: a change staged and unsaved in the context the plugin was activated
    /// over is not saved by it, and that context's later save does not touch the store's row.
    /// </summary>
    [Fact]
    public async Task InjectedStore_Write_DoesNotSaveCallersPendingChanges()
    {
        var pluginId = PluginActivationTestSeeds.NewId("wide");
        var pendingKey = $"test:pending:{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = await ActivateAsync(db, pluginId);
        db.Settings.Add(Setting.Create(pendingKey, "pending", null));

        await plugin.Store.SetAsync("k", "v", CancellationToken.None);

        using (var other = factory.Services.CreateScope())
        {
            var seen = other.ServiceProvider.GetRequiredService<AppDbContext>();
            (await seen.Settings.AnyAsync(s => s.Key == pendingKey)).Should().BeFalse("the store never saves the caller's context");
            (await seen.PluginDataEntries.CountAsync(e => e.PluginId == pluginId)).Should().Be(1);
        }

        db.Entry(db.Settings.Local.Single(s => s.Key == pendingKey)).State = EntityState.Detached;
        (await plugin.Store.GetAsync("k", CancellationToken.None)).Should().Be("v");
    }

    /// <summary>Purging a removed plugin deletes the rows its injected store wrote and keeps another plugin's.</summary>
    [Fact]
    public async Task Purge_RemovesInjectedStoreRows_KeepsOtherPlugin()
    {
        var pluginId = PluginActivationTestSeeds.NewId("wide");
        var otherId = PluginActivationTestSeeds.NewId("wide");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await (await ActivateAsync(db, pluginId)).Store.SetAsync("k", "v", CancellationToken.None);
            await (await ActivateAsync(db, otherId)).Store.SetAsync("k", "kept", CancellationToken.None);
        }

        await factory.Services.PublishAndWaitAsync(new PluginDataPurgeRequestedEvent(pluginId, DateTimeOffset.UtcNow));

        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.PluginDataEntries.CountAsync(e => e.PluginId == pluginId)).Should().Be(0);
        (await context.PluginDataScopes.CountAsync(s => s.PluginId == pluginId)).Should().Be(0);
        (await context.PluginDataEntries.CountAsync(e => e.PluginId == otherId)).Should().Be(1);
    }
}
