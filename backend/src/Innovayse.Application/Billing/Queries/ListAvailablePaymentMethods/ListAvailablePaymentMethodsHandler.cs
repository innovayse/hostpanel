namespace Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;

/// <summary>
/// Lists the payment gateways a payer can actually use: the built-in methods that are both
/// configured and switched on by the admin, plus every loaded payment plugin whose integration
/// is enabled and fully configured. Nothing is listed that would fail when the payer picks it,
/// and <c>PlaceOrderHandler</c> refuses any module this list does not contain, so listing and
/// ordering can never disagree.
/// </summary>
/// <param name="plugins">Plugin registry for loaded plugin manifests.</param>
/// <param name="pluginResolver">
/// Payment plugin resolver — the same gate <c>StartGatewayPaymentHandler</c> uses to decide
/// whether a module is usable at <c>start</c>.
/// </param>
/// <param name="settings">
/// Settings store, read for the admin's on/off switch on each built-in method. The admin
/// integrations page is where an operator turns a gateway off; a method whose switch is off
/// is not offered no matter what credentials the deployment carries.
/// </param>
/// <param name="stripe">Stripe service, asked whether the deployment carries a key at all.</param>
public sealed class ListAvailablePaymentMethodsHandler(
    IPluginRegistry plugins,
    IPaymentPluginResolver pluginResolver,
    ISettingRepository settings,
    IStripeService stripe)
{
    /// <summary>Builds the list of methods available right now.</summary>
    /// <param name="query">The query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Available methods, built-ins first, then plugins in load order.</returns>
    public async Task<IReadOnlyList<AvailablePaymentMethodDto>> HandleAsync(
        ListAvailablePaymentMethodsQuery query, CancellationToken ct)
    {
        var methods = new List<AvailablePaymentMethodDto>();

        // Configured is not enough on its own: an operator who switched Stripe off on the admin
        // integrations page still had it offered at checkout as long as the deployment carried
        // a secret key. Both gates apply, the way the plugin resolver applies them.
        if (stripe.IsConfigured
            && await IsIntegrationEnabledAsync(BuiltInPaymentModules.StripeIntegrationSlug, ct))
        {
            methods.Add(new(BuiltInPaymentModules.Stripe, "Credit/Debit Card (Stripe)"));
        }

        // Bank transfer used to be listed unconditionally, so switching it off on the admin
        // integrations page changed nothing at checkout and a deployment that takes cards only
        // still offered it. It has no credentials to be "configured" with, so the admin flag is
        // its only gate.
        if (await IsIntegrationEnabledAsync(BuiltInPaymentModules.BankTransferIntegrationSlug, ct))
        {
            methods.Add(new(BuiltInPaymentModules.BankTransfer, "Bank Transfer"));
        }

        foreach (var manifest in plugins.GetLoadedManifests().Where(m => m.Type == PluginType.Payment))
        {
            // Ask the resolver rather than re-deriving "enabled and configured" from settings by
            // hand — it is the same check StartGatewayPaymentHandler relies on to actually start
            // a payment.
            var plugin = await pluginResolver.ResolveAsync(manifest.Id, ct);
            if (plugin is not null)
            {
                methods.Add(new(manifest.Id, $"Bank Card ({manifest.Name})"));
            }
        }

        return methods;
    }

    /// <summary>
    /// Reads the admin's <c>is_enabled</c> flag for an integration. A missing setting counts as
    /// disabled, the way the admin integrations list treats it.
    /// </summary>
    /// <param name="slug">Integration slug as the admin integrations page keys it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the flag is stored as "true".</returns>
    private async Task<bool> IsIntegrationEnabledAsync(string slug, CancellationToken ct)
    {
        var flag = await settings.FindByKeyAsync(IntegrationSettingKeys.EnabledKey(slug), ct);
        return flag is not null && string.Equals(flag.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
