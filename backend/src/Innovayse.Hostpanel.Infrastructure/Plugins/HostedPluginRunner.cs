using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Starts every enabled hosted plugin once the host is up and stops them when it begins to shut down. A plugin
/// is hosted when its manifest declares <see cref="PluginType.Hosted"/> or its implementation also implements
/// <see cref="IHostedPlugin"/> beside another contract, as the SDK allows.
/// </summary>
/// <remarks>
/// <para>
/// Plugins are third-party code, so every one is isolated from the host and from each other: an activation, a
/// start or a stop that throws is logged with the plugin's id and that plugin is left out, while the host and
/// every other plugin carry on. A plugin that failed to start is never asked to stop.
/// </para>
/// <para>
/// The plugins start in <see cref="StartedAsync"/>, after every hosted service of the host (the web server among
/// them) has started, because the SDK promises a hosted plugin the host's own services are up. They stop in
/// <see cref="StoppingAsync"/>, before any of those services stop, so a plugin can still use them while it
/// winds down. Starts and stops run side by side, so one slow plugin does not hold the others back.
/// </para>
/// </remarks>
public sealed class HostedPluginRunner : IHostedLifecycleService
{
    /// <summary>Registry of plugins loaded at startup, read for the hosted ones.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Opens the scope the plugins are activated in and live in until they are stopped.</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Structured logger: the only record of a plugin that failed to start or stop.</summary>
    private readonly ILogger<HostedPluginRunner> _logger;

    /// <summary>
    /// The scope the running plugins were built in. Kept open while they run, because a plugin may hold scoped
    /// services from it; <see langword="null"/> before the start and after the stop.
    /// </summary>
    private AsyncServiceScope? _scope;

    /// <summary>The plugins that started and must be stopped, by plugin id; empty before the start and after the stop.</summary>
    private IReadOnlyDictionary<string, IHostedPlugin> _running = new Dictionary<string, IHostedPlugin>();

    /// <summary>Initializes a new instance of the <see cref="HostedPluginRunner"/> class.</summary>
    /// <param name="registry">Registry of plugins loaded at startup, read for the hosted ones.</param>
    /// <param name="scopeFactory">Opens the scope the plugins are activated in and live in until they are stopped.</param>
    /// <param name="logger">Structured logger: the only record of a plugin that failed to start or stop.</param>
    public HostedPluginRunner(PluginRegistry registry, IServiceScopeFactory scopeFactory, ILogger<HostedPluginRunner> logger)
    {
        _registry = registry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>No-op: the plugins wait until the host has started.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting.</param>
    /// <returns>A completed task.</returns>
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op: the plugins wait until the host has started.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Activates and starts every enabled hosted plugin, each failure isolated to its plugin.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting; handed to each plugin's start.</param>
    /// <returns>A task that completes when every plugin has started or failed to.</returns>
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        var hosted = _registry.LoadedPlugins
            .Where(plugin => plugin.Manifest.Type == PluginType.Hosted
                || typeof(IHostedPlugin).IsAssignableFrom(plugin.ImplementationType))
            .OrderBy(plugin => plugin.Manifest.Id, StringComparer.Ordinal)
            .ToList();
        if (hosted.Count == 0)
        {
            return;
        }

        var scope = _scopeFactory.CreateAsyncScope();
        _scope = scope;
        var activator = scope.ServiceProvider.GetRequiredService<PluginActivator>();

        // One at a time: the activations share the scope's data context, which runs one query at a time.
        var activated = new Dictionary<string, IHostedPlugin>(StringComparer.Ordinal);
        foreach (var plugin in hosted)
        {
            var instance = await TryActivateAsync(activator, plugin, cancellationToken);
            if (instance is not null)
            {
                activated[plugin.Manifest.Id] = instance;
            }
        }

        var started = await Task.WhenAll(activated.Select(plugin => TryStartAsync(plugin.Key, plugin.Value, cancellationToken)));
        _running = activated.Where((_, index) => started[index]).ToDictionary(StringComparer.Ordinal);
    }

    /// <summary>Stops every plugin that started, each failure isolated to its plugin, then releases their scope.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends; handed to each plugin's stop.</param>
    /// <returns>A task that completes when every plugin has stopped or failed to.</returns>
    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        var running = _running;
        _running = new Dictionary<string, IHostedPlugin>();
        await Task.WhenAll(running.Select(plugin => TryStopAsync(plugin.Key, plugin.Value, cancellationToken)));

        if (_scope is { } scope)
        {
            _scope = null;
            await scope.DisposeAsync();
        }
    }

    /// <summary>No-op: the plugins were stopped while the host was stopping.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op: the plugins were stopped while the host was stopping.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Activates one hosted plugin, logging instead of throwing when it cannot be.</summary>
    /// <param name="activator">The activator of the runner's scope.</param>
    /// <param name="plugin">The plugin to activate.</param>
    /// <param name="ct">Cancelled when the host gives up starting.</param>
    /// <returns>The instance when the plugin is enabled and was built; <see langword="null"/> otherwise.</returns>
    private async Task<IHostedPlugin?> TryActivateAsync(
        PluginActivator activator, LoadedPlugin plugin, CancellationToken ct)
    {
        try
        {
            var activation = await activator.ActivateAsync<IHostedPlugin>(plugin, ct);
            if (activation.Instance is null)
            {
                _logger.LogInformation(
                    "Hosted plugin '{Module}' was not started: {Outcome}.", activation.PluginId, activation.Outcome);
                return null;
            }

            return activation.Instance;
        }
        catch (Exception exception)
        {
            // A plugin's constructor is third-party code; whatever it throws stays with that plugin.
            _logger.LogError(exception, "Hosted plugin '{Module}' could not be activated.", plugin.Manifest.Id);
            return null;
        }
    }

    /// <summary>Starts one hosted plugin, logging instead of throwing when it fails.</summary>
    /// <param name="pluginId">The plugin id, for the log line.</param>
    /// <param name="plugin">The activated plugin.</param>
    /// <param name="ct">Cancelled when the host gives up starting.</param>
    /// <returns><see langword="true"/> when the plugin started and must be stopped later.</returns>
    private async Task<bool> TryStartAsync(string pluginId, IHostedPlugin plugin, CancellationToken ct)
    {
        try
        {
            await plugin.StartAsync(ct);
            _logger.LogInformation("Hosted plugin '{Module}' started.", pluginId);
            return true;
        }
        catch (Exception exception)
        {
            // Third-party code at the plugin boundary: a failed start must not stop the host or the other plugins.
            _logger.LogError(exception, "Hosted plugin '{Module}' failed to start.", pluginId);
            return false;
        }
    }

    /// <summary>Stops one hosted plugin, logging instead of throwing when it fails.</summary>
    /// <param name="pluginId">The plugin id, for the log line.</param>
    /// <param name="plugin">The running plugin.</param>
    /// <param name="ct">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A task that completes when the plugin has stopped or failed to.</returns>
    private async Task TryStopAsync(string pluginId, IHostedPlugin plugin, CancellationToken ct)
    {
        try
        {
            await plugin.StopAsync(ct);
            _logger.LogInformation("Hosted plugin '{Module}' stopped.", pluginId);
        }
        catch (Exception exception)
        {
            // Third-party code at the plugin boundary: a failed stop must not keep the others from stopping.
            _logger.LogError(exception, "Hosted plugin '{Module}' failed to stop.", pluginId);
        }
    }
}
