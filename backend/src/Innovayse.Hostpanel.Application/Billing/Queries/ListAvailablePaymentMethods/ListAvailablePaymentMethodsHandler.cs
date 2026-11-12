using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListAvailablePaymentMethods;

/// <summary>
/// Lists the payment gateways a payer can actually use: the built-in methods that are
/// switched on by the admin, plus every loaded payment plugin whose integration
/// is enabled and fully configured, and whose currency is the one this payer is billed in. A plugin that declares the
/// stored credentials capability in its manifest is marked, so checkout can offer to keep the payer's method.
/// Nothing is listed that would fail when the payer picks it, and <c>PlaceOrderHandler</c>
/// refuses any module this list does not contain, so listing and ordering can never disagree.
/// </summary>
/// <remarks>
/// The currency lookup is a shared question answered by <see cref="IPayerCurrencyResolver"/> and
/// <see cref="CurrencyExtensions"/> over the module's own data context; the settings lookup reads
/// the same context directly.
/// </remarks>
public sealed class ListAvailablePaymentMethodsHandler
{
    /// <summary>Plugin registry for loaded plugin manifests.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>
    /// Payment plugin resolver: the same gate <c>StartGatewayPaymentHandler</c> uses to decide
    /// whether a module is usable at <c>start</c>.
    /// </summary>
    private readonly IPaymentPluginResolver _pluginResolver;

    /// <summary>The module's data context, for the configured currencies.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    /// The one rule for which currency the caller is billed in: their client's, else the base.
    /// </summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The message bus, for the admin's on/off switch on each built-in method, owned by the integrations settings.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="plugins">Plugin registry for loaded plugin manifests.</param>
    /// <param name="pluginResolver">Payment plugin resolver.</param>
    /// <param name="context">The module's data context, for the configured currencies.</param>
    /// <param name="payerCurrency">Resolves the currency the caller is billed in.</param>
    /// <param name="bus">The message bus, for the admin's on/off switch on each built-in method.</param>
    public ListAvailablePaymentMethodsHandler(
        IPluginRegistry plugins,
        IPaymentPluginResolver pluginResolver,
        IHostpanelDbContext context,
        IPayerCurrencyResolver payerCurrency,
        IMessageBus bus)
    {
        _plugins = plugins;
        _pluginResolver = pluginResolver;
        _context = context;
        _payerCurrency = payerCurrency;
        _bus = bus;
    }

    /// <summary>Builds the list of methods available right now.</summary>
    /// <param name="query">The query; names the currency to list for, or none for the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Available methods, built-ins first, then plugins in load order: empty when the query
    /// names a currency the panel does not offer, since nothing can take money in it. Never
    /// fails.
    /// </returns>
    public async Task<Result<IReadOnlyList<AvailablePaymentMethodDto>>> HandleAsync(
        ListAvailablePaymentMethodsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var methods = new List<AvailablePaymentMethodDto>();

        // Settled first: a currency nobody can be billed in has no methods, whatever is switched on.
        var payerCurrencyNumeric = await ResolvePayerCurrencyNumericAsync(query.CurrencyCode, ct);
        if (payerCurrencyNumeric is null)
        {
            return Result.Success<IReadOnlyList<AvailablePaymentMethodDto>>(methods);
        }

        // Bank transfer has no credentials to be "configured" with: the admin's switch is its gate, and it also needs
        // something to show the payer, or the card would be empty and the payer could not pay.
        var bankTransfer = await _bus.LoadBankTransferSettingsAsync(ct);
        if (bankTransfer is { IsEnabled: true, HasPayerDetails: true })
        {
            methods.Add(new(BuiltInPaymentModules.BankTransfer, PaymentMethodNames.BankTransfer, SupportsStoredCredentials: false));
        }

        // A hosted gateway charges in one fixed currency, and StartGatewayPaymentHandler refuses
        // to start a payment whose invoice bills in another: it would otherwise hand the bank
        // "299" and have it read as 2.99 AMD for a $2.99 invoice. That refusal is right, but it
        // was landing *after* the payer had picked the method and pressed Place Order. The
        // currency is known here too, so a gateway that cannot take this payer's money is simply
        // not offered. The resolver that names the currency here is the one every invoice is
        // created with, so what is listed and what the invoice then bills in cannot drift.
        foreach (var manifest in _plugins.GetLoadedManifests().Where(m => m.Type == PluginType.Payment))
        {
            // Ask the resolver rather than re-deriving "enabled and configured" from settings by
            // hand: it is the same check StartGatewayPaymentHandler relies on to actually start
            // a payment.
            var plugin = await _pluginResolver.ResolveAsync(manifest.Id, ct);
            if (plugin is null)
            {
                continue;
            }

            if (!plugin.AcceptsCurrency(payerCurrencyNumeric))
            {
                continue;
            }

            methods.Add(new(
                manifest.Id,
                $"Bank Card ({manifest.Name})",
                manifest.Capabilities.Contains(PluginCapabilities.StoredCredentials, StringComparer.Ordinal)));
        }

        return Result.Success<IReadOnlyList<AvailablePaymentMethodDto>>(methods);
    }

    /// <summary>
    /// Works out which ISO 4217 numeric currency the list is for: the one the query names when it
    /// names one, otherwise the caller's; their client record's when they are signed in and
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
            return (await _payerCurrency.ForCallerAsync(ct)).Numeric;
        }

        return (await _context.FindOfferedCurrencyAsync(requested, ct))?.Numeric;
    }
}
