using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.SDK.Plugins.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/*
 * The status check is plugin code run on an admin's page load, so it must never hang the page, throw through to
 * the response or carry the plugin's internals out. The plugin goes through PluginActivator like every other
 * plugin call (switched on, configured, no pending migration), gets a token that fires at the host's limit, and
 * the host stops waiting at that same moment even when the plugin ignores the token. Every failure is a result
 * (fail closed: not working) and is logged by plugin id and exception type only, never the exception message,
 * which a plugin may fill with a URL, a key or a server's answer.
 */

/// <summary>Asks a loaded plugin's <see cref="IPluginStatus"/>, bounded by <see cref="PluginApiOptions.StatusTimeout"/>.</summary>
public sealed class PluginStatusProbe : IPluginStatusProbe
{
    /// <summary>Checks the activation gates and builds the plugin.</summary>
    private readonly PluginActivator _activator;

    /// <summary>The plugins loaded at startup, with their implementation types.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>The time limit of one check.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>Records a failed or late check by plugin id and exception type.</summary>
    private readonly ILogger<PluginStatusProbe> _logger;

    /// <summary>Initializes a new instance of the <see cref="PluginStatusProbe"/> class.</summary>
    /// <param name="activator">Checks the activation gates and builds the plugin.</param>
    /// <param name="registry">The plugins loaded at startup, with their implementation types.</param>
    /// <param name="options">The time limit of one check.</param>
    /// <param name="logger">Records a failed or late check by plugin id and exception type.</param>
    public PluginStatusProbe(
        PluginActivator activator,
        PluginRegistry registry,
        IOptions<PluginApiOptions> options,
        ILogger<PluginStatusProbe> logger)
    {
        _activator = activator;
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool Supports(string pluginId)
    {
        return Find(pluginId) is not null;
    }

    /// <inheritdoc />
    public async Task<PluginStatusProbeResult> ProbeAsync(string pluginId, CancellationToken ct)
    {
        var plugin = Find(pluginId);
        if (plugin is null)
        {
            return new PluginStatusProbeResult(PluginStatusProbeOutcome.NotSupported);
        }

        var prepared = await _activator.PrepareAsync<IPluginStatus>(plugin, ct);
        if (prepared.Outcome != PluginActivationOutcome.Activated || prepared.Create is null)
        {
            return new PluginStatusProbeResult(PluginStatusProbeOutcome.Inactive);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(_options.Value.StatusTimeout);
        try
        {
            var status = prepared.Create();

            // WaitAsync on the limit token: the host stops waiting at the limit even when the plugin ignores its token.
            var report = await status.GetStatusAsync(limit.Token).WaitAsync(limit.Token);
            return Read(pluginId, report);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The admin left: not the plugin's failure, so nothing is recorded or cached for it.
            throw;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            _logger.LogWarning("Status check of plugin {PluginId} gave no answer within the time limit.", pluginId);
            return new PluginStatusProbeResult(PluginStatusProbeOutcome.TimedOut);
        }
        catch (Exception ex)
        {
            // The type only: the message is plugin text and may carry a secret or an upstream answer.
            _logger.LogWarning("Status check of plugin {PluginId} failed with {ExceptionType}.", pluginId, ex.GetType().Name);
            return new PluginStatusProbeResult(PluginStatusProbeOutcome.Failed);
        }
    }

    /// <summary>The loaded plugin when it implements <see cref="IPluginStatus"/>, read from its type so no plugin code runs.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The plugin, or <see langword="null"/>.</returns>
    private LoadedPlugin? Find(string pluginId)
    {
        var plugin = _registry.Find(pluginId);
        return plugin is not null && typeof(IPluginStatus).IsAssignableFrom(plugin.ImplementationType) ? plugin : null;
    }

    /// <summary>Checks a report before the host believes it.</summary>
    /// <param name="pluginId">The plugin id, for the log.</param>
    /// <param name="report">What the plugin answered; a plugin compiled against another SDK may answer null.</param>
    /// <returns>The result; a missing report or an undefined health fails closed.</returns>
    private PluginStatusProbeResult Read(string pluginId, PluginStatusReport? report)
    {
        if (report is null || !Enum.IsDefined(report.Health))
        {
            _logger.LogWarning("Status check of plugin {PluginId} answered with no valid report.", pluginId);
            return new PluginStatusProbeResult(PluginStatusProbeOutcome.Failed);
        }

        var detail = report.Detail?.Trim();
        if (string.IsNullOrEmpty(detail))
        {
            detail = null;
        }
        else if (detail.Length > PluginStatusProbeResult.MaxDetailLength)
        {
            detail = detail[..PluginStatusProbeResult.MaxDetailLength];
        }

        return new PluginStatusProbeResult(PluginStatusProbeOutcome.Reported, report.Health, detail);
    }
}
