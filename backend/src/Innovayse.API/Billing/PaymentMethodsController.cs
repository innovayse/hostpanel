namespace Innovayse.API.Billing;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Returns available payment gateways for the checkout flow: the built-in methods
/// plus every loaded payment plugin whose integration is enabled and fully configured.
/// </summary>
/// <param name="plugins">Plugin registry for loaded plugin manifests.</param>
/// <param name="pluginResolver">
/// Payment plugin resolver — the same gate <c>StartGatewayPaymentHandler</c> uses to decide
/// whether a module is usable at <c>start</c>, so a plugin can never be listed here as
/// available and then refused when the payer actually tries to pay with it.
/// </param>
[ApiController]
[Route("api/payment-methods")]
[AllowAnonymous]
public sealed class PaymentMethodsController(
    IPluginRegistry plugins,
    IPaymentPluginResolver pluginResolver) : ControllerBase
{
    /// <summary>Lists all active payment gateways available at checkout.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of payment method objects with module name and display name.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var methods = new List<object>
        {
            new { module = BuiltInPaymentModules.Stripe, displayname = "Credit/Debit Card (Stripe)" },
            new { module = BuiltInPaymentModules.BankTransfer, displayname = "Bank Transfer" },
        };

        foreach (var manifest in plugins.GetLoadedManifests().Where(m => m.Type == PluginType.Payment))
        {
            // Ask the resolver rather than re-deriving "enabled and configured" from settings
            // by hand — it is the same check StartGatewayPaymentHandler relies on to actually
            // start a payment, so listing and starting can never disagree.
            var plugin = await pluginResolver.ResolveAsync(manifest.Id, ct);
            if (plugin is not null)
            {
                methods.Add(new { module = manifest.Id, displayname = $"Bank Card ({manifest.Name})" });
            }
        }

        return Ok(methods);
    }
}
