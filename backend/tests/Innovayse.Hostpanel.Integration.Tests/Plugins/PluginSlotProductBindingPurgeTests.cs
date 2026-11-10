using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.PurgePluginData;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The operator purge of a removed plugin (<see cref="PurgePluginDataCommand"/>, through the real bus) also deletes the
/// slot widget product bindings of that plugin written up to the moment the purge was asked for. The moment is a clock the
/// test holds still, and the binding stamps are set to exact values around it, so the <c>CreatedAt &lt;= OccurredAt</c>
/// boundary is checked at equal, earlier and later stamps; another plugin is left alone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginSlotProductBindingPurgeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>How long a purge may take to settle; generous because the first delivery compiles the handler chains.</summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

    /// <summary>A quota no seed reaches.</summary>
    private static readonly PluginStoreQuota Unbounded = new(int.MaxValue, long.MaxValue);

    /// <summary>
    /// A binding stamped before the purge moment and one stamped exactly at it are deleted, one stamped after it stays,
    /// and the bindings of another plugin stay.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Purge_DeletesBindingsUpToTheMoment_AndKeepsTheOnesWrittenAfterAsync()
    {
        var pluginId = $"purge-bind-{Guid.NewGuid():N}"[..40];
        var otherPluginId = $"keep-bind-{Guid.NewGuid():N}"[..40];
        // Whole microseconds, the precision of the column, so the equal stamp survives the round trip.
        var now = DateTimeOffset.UtcNow;
        var moment = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
        await BindAsync(pluginId, "before", 1, moment.AddSeconds(-1));
        await BindAsync(pluginId, "equal", 2, moment);
        await BindAsync(pluginId, "after", 3, moment.AddSeconds(1));
        await BindAsync(otherPluginId, "before", 1, moment.AddSeconds(-1));
        await OpenStoreScopeAsync(pluginId);
        using var host = new ClockedHost(factory, moment);

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(SettleTimeout)
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync(async Task (IMessageContext _) =>
            {
                var bus = host.Services.GetRequiredService<IMessageBus>();
                var outcome = await bus.InvokeAsync<Result<PluginActionResultDto>>(new PurgePluginDataCommand(pluginId));
                outcome.IsSuccess.Should().BeTrue();
            });

        (await WidgetsAsync(pluginId)).Should().Equal("after");
        (await WidgetsAsync(otherPluginId)).Should().Equal("before");
    }

    /// <summary>Writes one binding row and sets its <c>CreatedAt</c> to an exact moment, which the save stamps itself otherwise.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="widgetId">The widget id.</param>
    /// <param name="productId">The product id; no product row is needed, the table holds no foreign key.</param>
    /// <param name="createdAt">The stamp the row ends up with.</param>
    /// <returns>A task that completes when the row is saved and stamped.</returns>
    private async Task BindAsync(string pluginId, string widgetId, int productId, DateTimeOffset createdAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PluginSlotProductBindings.Add(PluginSlotProductBinding.Create(pluginId, "slot.a", widgetId, productId));
        await db.SaveChangesAsync(CancellationToken.None);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"PluginSlotProductBindings\" SET \"CreatedAt\" = {createdAt} WHERE \"PluginId\" = {pluginId} AND \"WidgetId\" = {widgetId}");
    }

    /// <summary>Opens a plugin store scope for the plugin, which is what tells the purge the plugin keeps data.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>A task that completes when the scope is saved.</returns>
    private async Task OpenStoreScopeAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dataScope = PluginDataScope.Open(pluginId, null);
        dataScope.Admit(1, PluginDataEntry.CharsOf("k", "v"), Unbounded);
        db.PluginDataScopes.Add(dataScope);
        db.PluginDataEntries.Add(PluginDataEntry.Create(pluginId, null, "k", "v"));
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Lists the widget ids a plugin still has bindings for.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The widget ids, ordered.</returns>
    private async Task<IReadOnlyList<string>> WidgetsAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginSlotProductBindings.AsNoTracking()
            .Where(b => b.PluginId == pluginId)
            .OrderBy(b => b.WidgetId)
            .Select(b => b.WidgetId)
            .ToListAsync();
    }
}
