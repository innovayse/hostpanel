using Hostpanel.Sdk.Plugins.Store;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/*
 * The plugin-wide store lives outside any call, so there is no request context it may write through: a store that shared
 * the activator's context would save whatever the caller had staged there, and the caller's later save would flush the
 * store's change. Each operation therefore opens its own scope, gets its own data context from it and disposes both when
 * it ends, so the store's write is its own unit of work and the caller's context is neither read nor saved. The rows,
 * the quota and the retry on a lost race are PluginStore's, unchanged.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The <see cref="IPluginStore"/> a plugin may take in its constructor: bound to the plugin and to no client, with every
/// operation run by a <see cref="PluginStore"/> over a data context of its own, never the caller's.
/// </summary>
internal sealed class PluginWideStore : IPluginStore
{
    /// <summary>The plugin whose values these are.</summary>
    private readonly string _pluginId;

    /// <summary>Opens the scope, and with it the data context, of one operation.</summary>
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Initializes the store of one plugin.</summary>
    /// <param name="pluginId">The plugin whose values these are.</param>
    /// <param name="scopes">Opens the scope, and with it the data context, of one operation.</param>
    public PluginWideStore(string pluginId, IServiceScopeFactory scopes)
    {
        _pluginId = pluginId;
        _scopes = scopes;
    }

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key, CancellationToken ct)
    {
        return RunAsync((store, token) => store.GetAsync(key, token), ct);
    }

    /// <inheritdoc/>
    public Task SetAsync(string key, string value, CancellationToken ct)
    {
        return RunAsync(
            async (store, token) =>
            {
                await store.SetAsync(key, value, token);
                return true;
            },
            ct);
    }

    /// <inheritdoc/>
    public Task<bool> RemoveAsync(string key, CancellationToken ct)
    {
        return RunAsync((store, token) => store.RemoveAsync(key, token), ct);
    }

    /// <summary>Runs one operation on a store over a context that exists only for it.</summary>
    /// <typeparam name="T">What the operation answers.</typeparam>
    /// <param name="operation">The operation, given the store to run on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the operation answered.</returns>
    private async Task<T> RunAsync<T>(Func<PluginStore, CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quota = scope.ServiceProvider.GetService<IOptions<PluginApiOptions>>()?.Value.StoreQuota
            ?? new PluginApiOptions().StoreQuota;

        // The quota signal is only read by a call runner; outside a call nobody reads it, the refusal is still thrown.
        return await operation(new PluginStore(_pluginId, null, db, quota, new PluginStoreQuotaSignal()), ct);
    }
}
