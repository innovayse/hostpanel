namespace Innovayse.API.Billing;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Returns available payment gateways for the checkout flow: the built-in methods
/// plus every loaded payment plugin whose integration is enabled and fully configured.
/// </summary>
/// <param name="plugins">Plugin registry for loaded plugin manifests.</param>
/// <param name="settings">Setting repository for integration configuration lookups.</param>
[ApiController]
[Route("api/payment-methods")]
[AllowAnonymous]
public sealed class PaymentMethodsController(
    IPluginRegistry plugins,
    ISettingRepository settings) : ControllerBase
{
    /// <summary>Lists all active payment gateways available at checkout.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of payment method objects with module name and display name.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var methods = new List<object>
        {
            new { module = "stripe", displayname = "Credit/Debit Card (Stripe)" },
            new { module = "bank_transfer", displayname = "Bank Transfer" },
        };

        var all = await settings.ListAsync(ct);
        var lookup = all.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in plugins.GetLoadedManifests().Where(m => m.Type == PluginType.Payment))
        {
            var prefix = $"integration:{manifest.Id}:";
            var enabled = lookup.TryGetValue($"{prefix}is_enabled", out var flag)
                && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
            var configured = manifest.Fields
                .Where(f => f.Required)
                .All(f => lookup.TryGetValue($"{prefix}{f.Key}", out var v) && !string.IsNullOrWhiteSpace(v));

            if (enabled && configured)
            {
                methods.Add(new { module = manifest.Id, displayname = $"Bank Card ({manifest.Name})" });
            }
        }

        return Ok(methods);
    }
}
