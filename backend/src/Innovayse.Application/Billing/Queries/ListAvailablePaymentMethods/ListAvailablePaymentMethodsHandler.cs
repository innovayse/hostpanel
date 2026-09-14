namespace Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Extensions;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;

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
/// <param name="payerCurrency">
/// The one rule for which currency the caller is billed in -- their client's, else the base --
/// shared with everything that creates an invoice, so a listed gateway and the invoice it will
/// be asked to pay cannot disagree.
/// </param>
/// <param name="currencies">
/// The configured currencies, for the one a caller names instead of their own.
/// </param>
public sealed class ListAvailablePaymentMethodsHandler(
    IPluginRegistry plugins,
    IPaymentPluginResolver pluginResolver,
    ISettingRepository settings,
    IStripeService stripe,
    IPayerCurrencyResolver payerCurrency,
    ICurrencyRepository currencies)
{
    /// <summary>Builds the list of methods available right now.</summary>
    /// <param name="query">The query; names the currency to list for, or none for the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Available methods, built-ins first, then plugins in load order — empty when the query
    /// names a currency the panel does not offer, since nothing can take money in it.
    /// </returns>
    public async Task<IReadOnlyList<AvailablePaymentMethodDto>> HandleAsync(
        ListAvailablePaymentMethodsQuery query, CancellationToken ct)
    {
        var methods = new List<AvailablePaymentMethodDto>();

        // Settled first: a currency nobody can be billed in has no methods, whatever is switched on.
        var payerCurrencyNumeric = await ResolvePayerCurrencyNumericAsync(query.CurrencyCode, ct);
        if (payerCurrencyNumeric is null)
        {
            return methods;
        }

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
        // not offered. The resolver that names the currency here is the one every invoice is
        // created with, so what is listed and what the invoice then bills in cannot drift.
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

            if (!string.Equals(payerCurrencyNumeric, plugin.CurrencyCode, StringComparison.Ordinal))
            {
                continue;
            }

            methods.Add(new(manifest.Id, $"Bank Card ({manifest.Name})"));
        }

        return methods;
    }

    /// <summary>
    /// Works out which ISO 4217 numeric currency the list is for: the one the query names when it
    /// names one, otherwise the caller's — their client record's when they are signed in and
    /// have one, the base currency for anyone else.
    /// </summary>
    /// <param name="requested">The alpha code the query named, or <see langword="null"/> for the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The numeric code of the configured currency, or <see langword="null"/> when the query
    /// named one the panel does not offer.
    /// </returns>
    private async Task<string?> ResolvePayerCurrencyNumericAsync(string? requested, CancellationToken ct)
    {
        if (requested is null)
        {
            return (await payerCurrency.ForCallerAsync(ct)).Numeric;
        }

        return (await currencies.FindOfferedAsync(requested, ct))?.Numeric;
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
