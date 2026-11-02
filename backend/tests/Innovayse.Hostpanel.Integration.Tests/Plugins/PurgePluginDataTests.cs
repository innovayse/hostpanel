using FluentValidation;
using Innovayse.Hostpanel.Application.Plugins.Commands.PurgePluginData;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary><see cref="PurgePluginDataCommand"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PurgePluginDataTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>More rows than the handler loads in one pass, so the purge has to run several batches.</summary>
    private const int RowsOverOneBatch = 1201;

    /// <summary>Every scope of the plugin is purged (plugin wide and two clients) and another plugin's rows are left.</summary>
    [Fact]
    public async Task HandleAsync_PluginWithSeveralScopes_RemovesOnlyThatPluginsRowsAsync()
    {
        var pluginId = NewPluginId();
        var otherPluginId = NewPluginId();
        await SeedAsync(
            PluginDataEntry.Create(pluginId, null, "settings", "wide"),
            PluginDataEntry.Create(pluginId, "client-a", "cursor", "1"),
            PluginDataEntry.Create(pluginId, "client-b", "cursor", "2"),
            PluginDataEntry.Create(otherPluginId, null, "settings", "kept"),
            PluginDataEntry.Create(otherPluginId, "client-a", "cursor", "kept"));

        var result = await PurgeAsync(pluginId);

        result.IsSuccess.Should().BeTrue();
        (await CountAsync(pluginId)).Should().Be(0);
        (await CountAsync(otherPluginId)).Should().Be(2);
    }

    /// <summary>A plugin holding more rows than one batch has every row removed.</summary>
    [Fact]
    public async Task HandleAsync_MoreRowsThanOneBatch_RemovesAllOfThemAsync()
    {
        var pluginId = NewPluginId();
        var rows = Enumerable.Range(0, RowsOverOneBatch)
            .Select(i => PluginDataEntry.Create(pluginId, "client-a", $"key-{i}", "value"))
            .ToArray();
        await SeedAsync(rows);
        (await CountAsync(pluginId)).Should().Be(RowsOverOneBatch);

        var result = await PurgeAsync(pluginId);

        result.IsSuccess.Should().BeTrue();
        (await CountAsync(pluginId)).Should().Be(0);
    }

    /// <summary>A plugin with no rows succeeds, and so does a repeat, because the purge is idempotent.</summary>
    [Fact]
    public async Task HandleAsync_PluginWithNoRows_SucceedsEveryTimeAsync()
    {
        var pluginId = NewPluginId();

        (await PurgeAsync(pluginId)).IsSuccess.Should().BeTrue();
        (await PurgeAsync(pluginId)).IsSuccess.Should().BeTrue();
    }

    /// <summary>A plugin id that is not well formed is refused by the validator with a <c>ValidationException</c> and nothing is removed.</summary>
    [Fact]
    public async Task HandleAsync_InvalidPluginId_IsRefusedByTheValidatorAsync()
    {
        var pluginId = NewPluginId();
        await SeedAsync(PluginDataEntry.Create(pluginId, null, "settings", "kept"));

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await FluentActions
            .Invoking(() => bus.InvokeAsync<Result>(new PurgePluginDataCommand("Not A Plugin!")))
            .Should()
            .ThrowAsync<ValidationException>();
        (await CountAsync(pluginId)).Should().Be(1);
    }

    /// <summary>Makes a plugin id no other test uses, in the shape the plugin id rule accepts.</summary>
    /// <returns>A lower-case id with hyphens.</returns>
    private static string NewPluginId()
    {
        return $"purge-{Guid.NewGuid():N}";
    }

    /// <summary>Saves the rows in their own scope.</summary>
    /// <param name="rows">The store rows to insert.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedAsync(params PluginDataEntry[] rows)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.PluginDataEntries.AddRange(rows);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Counts the store rows of one plugin in a fresh scope, so nothing is read from a tracked copy.</summary>
    /// <param name="pluginId">The plugin whose rows are counted.</param>
    /// <returns>How many rows the plugin keeps.</returns>
    private async Task<int> CountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.PluginDataEntries.CountAsync(e => e.PluginId == pluginId);
    }

    /// <summary>Dispatches the command through the bus in its own scope.</summary>
    /// <param name="pluginId">The plugin whose rows are purged.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> PurgeAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        return await bus.InvokeAsync<Result>(new PurgePluginDataCommand(pluginId));
    }
}
