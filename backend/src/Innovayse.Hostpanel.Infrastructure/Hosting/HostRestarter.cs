using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Hosting;

/// <summary>
/// Restarts the host by stopping it: after a short pause the web server stops taking connections, the requests and
/// background jobs that run finish within the host's shutdown timeout (<c>Plugins:DrainSeconds</c>) and the process exits
/// with <see cref="ExitCode"/>, after which the container's restart policy starts it again.
/// </summary>
public sealed class HostRestarter : IHostRestarter
{
    /// <summary>
    /// The exit code of a restart the host asked for itself (the sysexits code for a temporary failure), so a supervisor
    /// can tell it from a crash.
    /// </summary>
    public const int ExitCode = 75;

    /// <summary>The host's lifetime, whose stop starts the graceful shutdown.</summary>
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>The pause between the request for a restart and the start of the stop.</summary>
    private readonly TimeSpan _delay;

    /// <summary>Records the restart.</summary>
    private readonly ILogger<HostRestarter> _logger;

    /// <summary>Set to 1 by the first request for a restart; later requests find it set and do nothing.</summary>
    private int _requested;

    /// <summary>Set to 1 once the stop has started, which is when the health probe starts to report draining.</summary>
    private int _draining;

    /// <summary>Initializes the restarter.</summary>
    /// <param name="lifetime">The host's lifetime, whose stop starts the graceful shutdown.</param>
    /// <param name="options">Holds the pause between the request and the stop.</param>
    /// <param name="logger">Records the restart.</param>
    public HostRestarter(IHostApplicationLifetime lifetime, IOptions<PluginOptions> options, ILogger<HostRestarter> logger)
    {
        _lifetime = lifetime;
        _delay = TimeSpan.FromSeconds(options.Value.RestartDelaySeconds);
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsRestarting => Volatile.Read(ref _draining) == 1;

    /// <inheritdoc/>
    public void Restart()
    {
        if (Interlocked.Exchange(ref _requested, 1) == 1)
        {
            return;
        }

        _ = StopAfterDelayAsync();
    }

    /// <summary>Waits the pause, then starts the graceful stop with the restart exit code.</summary>
    /// <returns>A task that completes when the stop has started.</returns>
    private async Task StopAfterDelayAsync()
    {
        await Task.Delay(_delay);
        _logger.LogInformation("Restarting the host: draining running requests, then exiting with code {ExitCode}", ExitCode);
        Environment.ExitCode = ExitCode;
        Volatile.Write(ref _draining, 1);
        _lifetime.StopApplication();
    }
}
