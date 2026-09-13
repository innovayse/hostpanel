namespace Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Options;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Options;

/// <summary>
/// Lists the payment gateways a payer can actually use: the built-in methods that are both
/// configured and switched on by the admin, plus every loaded payment plugin whose integration
/// is enabled and fully configured, and whose currency is the one this payer is billed in.
/// Nothing is listed that would fail when the payer picks it, and <c>PlaceOrderHandler</c>
/// refuses any module this list does not contain, so listing and ordering can never disagree.
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
/// <param name="caller">The caller, so a signed-in client's own billing currency can be read.</param>
/// <param name="clientRepo">Client repository, for that currency.</param>
/// <param name="billingOptions">Panel billing defaults, for the currency a guest or an unset client is billed in.</param>
public sealed class ListAvailablePaymentMethodsHandler(
    IPluginRegistry plugins,
    IPaymentPluginResolver pluginResolver,
    ISettingRepository settings,
    IStripeService stripe,
    ICurrentRequestContext caller,
    IClientRepository clientRepo,
    IOptions<BillingOptions> billingOptions)
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

        // A hosted gateway charges in one fixed currency, and StartGatewayPaymentHandler refuses
        // to start a payment whose invoice bills in another -- it would otherwise hand the bank
        // "299" and have it read as 2.99 AMD for a $2.99 invoice. That refusal is right, but it
        // was landing *after* the payer had picked the method and pressed Place Order. The
        // currency is known here too, so a gateway that cannot take this payer's money is simply
        // not offered. Same rule, same helper, so the two cannot drift.
        var payerCurrencyNumeric = await ResolvePayerCurrencyNumericAsync(ct);

        foreach (var manifest in plugins.GetLoadedManifests().Where(m => m.Type == PluginType.Payment))
        {
            // Ask the resolver rather than re-deriving "enabled and configured" from settings by
            // hand — it is the same check StartGatewayPaymentHandler relies on to actually start
            // a payment.
            var plugin = await pluginResolver.ResolveAsync(manifest.Id, ct);
            if (plugin is null)
            {
                continue;
            }

            // A payer whose currency has no ISO numeric mapping cannot pay through any gateway --
            // the start would refuse for that very reason -- so none is listed for them.
            if (payerCurrencyNumeric is null
                || !string.Equals(payerCurrencyNumeric, plugin.CurrencyCode, StringComparison.Ordinal))
            {
                continue;
            }

            methods.Add(new(manifest.Id, $"Bank Card ({manifest.Name})"));
        }

        return methods;
    }

    /// <summary>
    /// Works out which ISO 4217 numeric currency the caller is billed in: their client record's
    /// when they are signed in and have one, the panel default otherwise (a guest at checkout is
    /// billed in the default, and that is the currency the order they are about to place will
    /// carry).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The numeric code, or <see langword="null"/> when the alpha code has no mapping.</returns>
    private async Task<string?> ResolvePayerCurrencyNumericAsync(CancellationToken ct)
    {
        string? clientCurrency = null;
        if (caller.UserId is { } userId)
        {
            var client = await clientRepo.FindByUserIdAsync(userId, ct);
            clientCurrency = client?.Currency;
        }

        var alpha = CurrencyCodes.ResolvePayerCurrency(clientCurrency, billingOptions.Value.DefaultCurrency);
        return CurrencyCodes.ToNumeric(alpha);
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
