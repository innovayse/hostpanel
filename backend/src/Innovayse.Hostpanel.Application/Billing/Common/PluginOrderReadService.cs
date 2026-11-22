using Innovayse.Hostpanel.Application.Billing.Commands.ReadGatewayOrder;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

/*
 * The payment half of the plugin host API (#1356), an Application service for the same reason PluginSshAccessService is: the
 * host API is an Infrastructure adapter and an adapter never dispatches a handler, so it hands this service the call's
 * binding and this service dispatches.
 *
 * A callback is a hint, never proof, and the SDK says the host may ignore it and never tells the plugin what it did. So the
 * checks run in a fixed order, cheapest first, and every refusal is silent (a Debug line with the plugin id, never the order
 * id, which a caller chose): (1) the shape of the order id; (2) the plugin's budget for that order, which fails closed, so a
 * flood of callbacks cannot become a flood of gateway calls; (3) the use case, whose own failure and any exception are
 * swallowed too, because the plugin's callback must answer the gateway whatever the host found. Only the caller's own
 * cancellation propagates.
 */

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Serves <c>IPluginHostPaymentApi.RequestOrderReadAsync</c>: a payment plugin's callback asks the host to read one gateway
/// order now. The service never reports an outcome to the plugin and never settles anything itself: the read runs through
/// <see cref="ReadGatewayOrderCommand"/>, which leaves the decision to the plugin's own status read.
/// </summary>
public sealed class PluginOrderReadService
{
    /// <summary>Takes the plugin's budget for reading one order.</summary>
    private readonly IPluginHostAccessLimiter _limiter;

    /// <summary>Wolverine message bus the read is dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger; records what was ignored, with the plugin id.</summary>
    private readonly ILogger<PluginOrderReadService> _logger;

    /// <summary>Initializes the service.</summary>
    /// <param name="limiter">Takes the plugin's budget for reading one order.</param>
    /// <param name="bus">Wolverine message bus the read is dispatched through.</param>
    /// <param name="logger">Structured logger.</param>
    public PluginOrderReadService(IPluginHostAccessLimiter limiter, IMessageBus bus, ILogger<PluginOrderReadService> logger)
    {
        _limiter = limiter;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Asks the host to read one gateway order of the calling plugin, or ignores the request.</summary>
    /// <param name="binding">The call; its plugin id is the only owner the order is looked up under.</param>
    /// <param name="gatewayOrderId">The gateway-side order id the plugin sent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the read finished or the request was ignored; it never carries an outcome.</returns>
    public async Task RequestAsync(PluginCallBinding binding, string gatewayOrderId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (!GatewayOrderIds.IsWellFormed(gatewayOrderId))
        {
            _logger.LogDebug("Plugin {PluginId} asked to read an order id that is not well formed; ignored.", binding.PluginId);
            return;
        }

        var key = PluginHostAccessKey.ForOrder(binding.PluginId, gatewayOrderId, RateLimitPolicies.PluginOrderRead);
        if (!await _limiter.TryAcquireAsync(key, ct))
        {
            _logger.LogDebug("Plugin {PluginId} spent its budget for reading one order; ignored.", binding.PluginId);
            return;
        }

        try
        {
            var read = await _bus.InvokeAsync<Result>(new ReadGatewayOrderCommand(binding.PluginId, gatewayOrderId), ct);
            if (read.IsFailure)
            {
                _logger.LogDebug("The read a plugin asked for ended with {Code}; ignored.", read.ErrorCode);
            }
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // The plugin's callback answers the gateway whatever the host found, so nothing here reaches it.
            _logger.LogWarning(ex, "The read of a gateway order that plugin {PluginId} asked for failed; ignored.", binding.PluginId);
        }
    }
}
