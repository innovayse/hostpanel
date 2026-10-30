using Innovayse.SDK.Plugins.Hosting;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>A well-behaved hosted plugin: records its start and its stop.</summary>
internal sealed class RecordingHostedPlugin : IHostedPlugin
{
    /// <summary>Where the start and stop are recorded.</summary>
    private readonly HostedPluginJournal _journal;

    /// <summary>Initializes the plugin.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    /// <param name="journal">Where the start and stop are recorded.</param>
    public RecordingHostedPlugin(IConfiguration configuration, HostedPluginJournal journal)
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
        return Task.CompletedTask;
    }
}
