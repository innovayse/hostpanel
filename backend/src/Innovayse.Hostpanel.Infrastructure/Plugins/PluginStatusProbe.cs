using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Lifecycle;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
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

/// <summary>
/// Asks a loaded plugin whether it works, bounded by <see cref="PluginApiOptions.StatusTimeout"/>: through its
/// <see cref="IPluginStatus"/> when it implements one, and for a registrar plugin through its own connection test
/// (<see cref="IRegistrarOperationsPlugin.TestConnectionAsync"/>) with the connection built from its saved settings.
/// </summary>
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

        return typeof(IPluginStatus).IsAssignableFrom(plugin.ImplementationType)
            ? await RunAsync<IPluginStatus>(
                plugin, async (status, _, token) => Read(pluginId, await status.GetStatusAsync(token)), ct)
            : await RunAsync<IRegistrarOperationsPlugin>(
                plugin, async (registrar, settings, token) => ReadConnection(await registrar.TestConnectionAsync(
                    new RegistrarTestConnectionContext(RegistrarConnectionBuilder.Build(plugin.Manifest, settings)),
                    token)), ct);
    }

    /// <summary>
    /// Runs one check the way every check is run: through the activation gates, with a limit the host stops waiting at, and
    /// with every failure turned into a result.
    /// </summary>
    /// <typeparam name="TContract">The SDK contract the check calls the plugin through.</typeparam>
    /// <param name="plugin">The loaded plugin.</param>
    /// <param name="check">The call to the plugin, given the plugin, the settings it was built with and the limit token.</param>
    /// <param name="ct">Cancellation token of the admin's request.</param>
    /// <returns>The check's result, or the outcome that says why it could not give one.</returns>
    private async Task<PluginStatusProbeResult> RunAsync<TContract>(
        LoadedPlugin plugin,
        Func<TContract, IReadOnlyDictionary<string, string?>, CancellationToken, Task<PluginStatusProbeResult>> check,
        CancellationToken ct)
        where TContract : class
    {
        var pluginId = plugin.Manifest.Id;
        var prepared = await _activator.PrepareAsync<TContract>(plugin, ct);
        if (prepared.Outcome != PluginActivationOutcome.Activated || prepared.Create is null || prepared.Settings is null)
        {
            return new PluginStatusProbeResult(PluginStatusProbeOutcome.Inactive);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(_options.Value.StatusTimeout);
        try
        {
            var instance = prepared.Create();

            // WaitAsync on the limit token: the host stops waiting at the limit even when the plugin ignores its token.
            return await check(instance, prepared.Settings, limit.Token).WaitAsync(limit.Token);
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

    /// <summary>
    /// The loaded plugin when it implements <see cref="IPluginStatus"/>, or is a registrar plugin that offers operations,
    /// read from its type and manifest so no plugin code runs.
    /// </summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The plugin, or <see langword="null"/>.</returns>
    private LoadedPlugin? Find(string pluginId)
    {
        var plugin = _registry.Find(pluginId);
        if (plugin is null)
        {
            return null;
        }

        var isRegistrar = plugin.Manifest.Type == PluginType.Registrar
            && plugin.Manifest.Declares(PluginCapabilities.RegistrarOperations)
            && typeof(IRegistrarOperationsPlugin).IsAssignableFrom(plugin.ImplementationType);

        return typeof(IPluginStatus).IsAssignableFrom(plugin.ImplementationType) || isRegistrar ? plugin : null;
    }

    /// <summary>Turns a registrar plugin's connection test into the report the status checks share.</summary>
    /// <param name="test">What the plugin answered.</param>
    /// <returns>A healthy report when it connected, otherwise an unavailable one; the host words why from the code.</returns>
    private static PluginStatusProbeResult ReadConnection(PanelConnectionTestResult test)
    {
        return test.Outcome == PanelConnectionOutcome.Connected
            ? new PluginStatusProbeResult(PluginStatusProbeOutcome.Reported, PluginHealth.Healthy)
            : new PluginStatusProbeResult(PluginStatusProbeOutcome.Reported, PluginHealth.Unavailable);
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
