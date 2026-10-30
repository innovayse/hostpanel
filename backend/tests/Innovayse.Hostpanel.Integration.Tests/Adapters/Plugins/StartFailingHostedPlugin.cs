using Innovayse.SDK.Plugins.Hosting;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>A hosted plugin whose start throws; records a stop, which the runner must never ask for.</summary>
internal sealed class StartFailingHostedPlugin : IHostedPlugin
{
    /// <summary>Where a stop would be recorded.</summary>
    private readonly HostedPluginJournal _journal;

    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    /// <param name="journal">Where a stop would be recorded.</param>
    public StartFailingHostedPlugin(IConfiguration configuration, HostedPluginJournal journal)
    {
        _journal = journal;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct)
    {
        // Yields first, so the failure arrives as a faulted task the way an async plugin's would.
        await Task.Yield();
        throw new InvalidOperationException("The worker could not start.");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct)
    {
        _journal.RecordStopped(this);
        return Task.CompletedTask;
    }
}
