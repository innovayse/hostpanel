using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Resolves a loaded payment plugin into a ready-to-use <see cref="IPaymentPlugin"/> instance. The gates and
/// the configuration the plugin receives are <see cref="PluginActivator"/>'s; this adapter only picks the
/// plugin by module id and logs why a module the caller named resolved to nothing.
/// </summary>
public sealed class PaymentPluginResolver : IPaymentPluginResolver
{
    /// <summary>Turns the chosen plugin into an instance once its settings allow it.</summary>
    private readonly PluginActivator _activator;

    /// <summary>Registry of plugins loaded at startup.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>Structured logger: a null result is ambiguous to callers, so each rejection gate logs why.</summary>
    private readonly ILogger<PaymentPluginResolver> _logger;

    /// <summary>Initializes a new instance of the <see cref="PaymentPluginResolver"/> class.</summary>
    /// <param name="activator">Turns the chosen plugin into an instance once its settings allow it.</param>
    /// <param name="registry">Registry of plugins loaded at startup.</param>
    /// <param name="logger">Structured logger: a null result is ambiguous to callers, so each rejection gate logs why.</param>
    public PaymentPluginResolver(PluginActivator activator, PluginRegistry registry, ILogger<PaymentPluginResolver> logger)
    {
        _activator = activator;
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IPaymentPlugin?> ResolveAsync(string module, CancellationToken ct)
    {
        var plugin = _registry.Find(module);
        if (plugin is null || plugin.Manifest.Type != PluginType.Payment)
        {
            _logger.LogWarning(
                "Payment plugin '{Module}' is not installed or is not a payment-type plugin.", module);
            return null;
        }

        var activation = await _activator.ActivateAsync<IPaymentPlugin>(plugin, ct);

        // A caller named this module to take a payment with it, so a switched-off gateway is worth a warning
        // here, unlike a fulfilment plugin left off on purpose.
        if (activation.Outcome == PluginActivationOutcome.Disabled)
        {
            _logger.LogWarning("Payment plugin '{Module}' is disabled.", module);
        }

        return activation.Instance;
    }
}
