using System.Security.Authentication;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one place that turns a panel plugin into a contract instance for a call and runs the call. It asks
/// <see cref="PluginActivator"/> like every other plugin call (switched on, configured, no pending migration, declared capabilities
/// implemented), answers <c>PANEL_PLUGIN_UNAVAILABLE</c> when the plugin cannot be activated, and hides everything a failing plugin
/// says behind <see cref="PanelPluginCallException"/>. A call is bounded by the plugin call time limit
/// (<see cref="PluginApiOptions.CallTimeoutSeconds"/>): past it the plugin's token is cancelled, the host stops waiting even for a
/// plugin that ignores the token, and the call fails closed as a <see cref="PanelPluginCallException"/>.
/// </summary>
public sealed class PanelPluginCaller
{
    /// <summary>Finds a plugin by id.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Turns a plugin into an instance once its settings allow it.</summary>
    private readonly PluginActivator _activator;

    /// <summary>The plugin call time limit.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>Records why a plugin could not be activated or called, by plugin id and exception type only.</summary>
    private readonly ILogger<PanelPluginCaller> _logger;

    /// <summary>Initializes a new instance of the <see cref="PanelPluginCaller"/> class.</summary>
    /// <param name="registry">Finds a plugin by id.</param>
    /// <param name="activator">Turns a plugin into an instance once its settings allow it.</param>
    /// <param name="options">The plugin call time limit.</param>
    /// <param name="logger">Records why a plugin could not be activated or called, by plugin id and exception type only.</param>
    public PanelPluginCaller(
        PluginRegistry registry, PluginActivator activator, IOptions<PluginApiOptions> options, ILogger<PanelPluginCaller> logger)
    {
        _registry = registry;
        _activator = activator;
        _options = options;
        _logger = logger;
    }

    /// <summary>Tells whether a loaded plugin declares a capability in its manifest.</summary>
    /// <param name="pluginId">The plugin's id.</param>
    /// <param name="capability">A <c>PluginCapabilities</c> name.</param>
    /// <returns><see langword="true"/> when the plugin is loaded and declares the capability.</returns>
    public bool Declares(string pluginId, string capability)
    {
        return _registry.Find(pluginId)?.Manifest.Declares(capability) ?? false;
    }

    /// <summary>Activates the plugin as a contract and runs one call on it.</summary>
    /// <typeparam name="TContract">The contract the call needs: the panel contract, or the login contract for login details.</typeparam>
    /// <typeparam name="TResult">What the call answers.</typeparam>
    /// <param name="pluginId">The serving plugin's id.</param>
    /// <param name="operation">The contract method name, for the log and the exception.</param>
    /// <param name="call">The call on the activated plugin, given the token it must honour: the caller's, cancelled at the time limit too.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the plugin answered.</returns>
    /// <exception cref="DomainRuleException">Thrown with <c>PANEL_PLUGIN_UNAVAILABLE</c> when the plugin is not loaded, off, unconfigured, migrating or broken.</exception>
    /// <exception cref="PanelPluginCallException">Thrown when the plugin threw or did not answer within the time limit; only the type reaches the exception.</exception>
    public async Task<TResult> RunAsync<TContract, TResult>(
        string pluginId, string operation, Func<TContract, CancellationToken, Task<TResult>> call, CancellationToken ct)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(call);

        var plugin = _registry.Find(pluginId);
        if (plugin is null)
        {
            _logger.LogWarning("Panel plugin {PluginId} is not loaded, so {Operation} was not called.", pluginId, operation);
            throw Unavailable();
        }

        var prepared = await _activator.PrepareAsync<TContract>(plugin, ct);
        if (prepared.Outcome is not PluginActivationOutcome.Activated || prepared.Create is null)
        {
            _logger.LogWarning(
                "Panel plugin {PluginId} is not available for {Operation}: {Outcome}.", pluginId, operation, prepared.Outcome);
            throw Unavailable();
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(_options.Value.CallTimeoutFor(null));
        try
        {
            // WaitAsync on the limit token: the host stops waiting at the limit even when the plugin ignores its token.
            return await call(prepared.Create(), limit.Token).WaitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller left: not the plugin's failure, so nothing is recorded for it.
            throw;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            _logger.LogWarning("Panel plugin {PluginId} gave no answer to {Operation} within the time limit.", pluginId, operation);
            throw new PanelPluginCallException(pluginId, operation, nameof(TimeoutException), PanelPluginFailure.TimedOut);
        }
        catch (Exception ex)
        {
            // The type only: the message is plugin text and may carry a secret or an upstream answer.
            _logger.LogWarning(
                "Panel plugin {PluginId} failed in {Operation} with {ExceptionType}.", pluginId, operation, ex.GetType().Name);
            throw new PanelPluginCallException(pluginId, operation, ex.GetType().Name, Classify(ex));
        }
    }

    /// <summary>
    /// Tells the kind of failure from the type of what was thrown and of what it wraps, never from a message: a timeout (a plugin's own
    /// timeout, or an HTTP client's that wraps one) and a failed secure connection are the two the admin can act on.
    /// </summary>
    /// <param name="ex">What the plugin threw.</param>
    /// <returns>The kind of failure; <see cref="PanelPluginFailure.Failed"/> when it is neither.</returns>
    private static PanelPluginFailure Classify(Exception ex)
    {
        for (var inner = ex; inner is not null; inner = inner.InnerException)
        {
            if (inner is TimeoutException)
            {
                return PanelPluginFailure.TimedOut;
            }

            if (inner is AuthenticationException)
            {
                return PanelPluginFailure.TlsRejected;
            }
        }

        return PanelPluginFailure.Failed;
    }

    /// <summary>Builds the refusal every unactivatable plugin answers with.</summary>
    /// <returns>The <c>PANEL_PLUGIN_UNAVAILABLE</c> refusal.</returns>
    private static DomainRuleException Unavailable()
    {
        return new DomainRuleException(
            HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE, "The plugin that serves the server's control panel is not available.");
    }
}
