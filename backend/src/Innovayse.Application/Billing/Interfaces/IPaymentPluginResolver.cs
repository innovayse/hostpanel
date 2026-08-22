namespace Innovayse.Application.Billing.Interfaces;

using Innovayse.SDK.Plugins;

/// <summary>
/// Resolves a configured payment gateway plugin by its module id.
/// Implemented in Infrastructure: reads the integration settings saved from the
/// admin UI and instantiates the plugin with a configuration built from them.
/// </summary>
public interface IPaymentPluginResolver
{
    /// <summary>
    /// Resolves the plugin for the given module.
    /// </summary>
    /// <param name="module">The plugin id (e.g. "innovayse-inecobank").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The ready-to-use plugin, or <see langword="null"/> when the plugin is not loaded,
    /// is not a payment plugin, is disabled, or its required settings are missing.
    /// </returns>
    Task<IPaymentPlugin?> ResolveAsync(string module, CancellationToken ct);
}
