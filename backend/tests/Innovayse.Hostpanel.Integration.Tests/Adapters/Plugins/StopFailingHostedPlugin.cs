using Innovayse.SDK.Plugins.Hosting;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>A hosted plugin that starts normally and throws when asked to stop.</summary>
internal sealed class StopFailingHostedPlugin : IHostedPlugin
{
    /// <summary>Where the start and the stop request are recorded.</summary>
    private readonly HostedPluginJournal _journal;

    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    /// <param name="journal">Where the start and the stop request are recorded.</param>
    public StopFailingHostedPlugin(IConfiguration configuration, HostedPluginJournal journal)
    {
        _journal = journal;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken ct)
    {
        _journal.RecordStarted(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct)
    {
        _journal.RecordStopped(this);
        throw new InvalidOperationException("The worker could not stop.");
    }
}
