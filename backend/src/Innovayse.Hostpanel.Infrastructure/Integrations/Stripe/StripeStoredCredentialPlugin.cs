using System.Globalization;
using global::Stripe;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe.Settings;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;

/// <summary>
/// The built-in Stripe gateway's stored credential capability: Stripe customers, SetupIntents that save a card,
/// off-session PaymentIntents and the look-up of a charge by its idempotency key. The host reaches it only through
/// <see cref="IStoredCredentialPlugin"/>, exactly as it would reach a payment plugin, so nothing outside
/// <c>Integrations/Stripe</c> knows it is Stripe.
/// </summary>
/// <remarks>
/// Follows the contract's rules: a refusal of a charge is <see cref="StoredChargeOutcome.Declined"/>, never an
/// exception; anything that leaves the outcome unknown (the gateway was not reached, an idempotency conflict, a
/// server error) is thrown, and the host then keeps the invoice held until it has looked the charge up.
/// </remarks>
public sealed class StripeStoredCredentialPlugin : IStoredCredentialPlugin
{
    /// <summary>The payment-intent status Stripe reports once the charge has gone through.</summary>
    private const string SucceededStatus = "succeeded";

    /// <summary>The payment-intent status of a charge the cardholder must authenticate.</summary>
    private const string RequiresActionStatus = "requires_action";

    /// <summary>The payment-intent status of a charge Stripe accepted and has not settled.</summary>
    private const string ProcessingStatus = "processing";

    /// <summary>The error type Stripe answers a refused card with.</summary>
    private const string CardErrorType = "card_error";

    /// <summary>The error code of a card that needs the cardholder to authenticate.</summary>
    private const string AuthenticationRequiredCode = "authentication_required";

    /// <summary>The setup usage that lets a saved card be charged without the cardholder.</summary>
    private const string OffSessionUsage = "off_session";

    /// <summary>The payment method type this capability saves.</summary>
    private const string CardMethodType = "card";

    /// <summary>The metadata key the invoice reference rides on.</summary>
    private const string InvoiceReferenceMetadataKey = "invoiceId";

    /// <summary>The metadata key the idempotency key rides on, so a charge can be found by it again.</summary>
    private const string IdempotencyMetadataKey = "attemptKey";

    /// <summary>The metadata key the host's client reference rides on, so a customer can be found again.</summary>
    private const string CustomerClientMetadataKey = "hostpanelClientId";

    /// <summary>
    /// How far back a charge is looked up. A charge older than this belongs to an attempt the host reconciled long
    /// ago, so listing the customer's whole history to find it would only make a lookup slower and a mistaken match likelier.
    /// </summary>
    private static readonly TimeSpan LookupWindow = TimeSpan.FromDays(14);

    /// <summary>Stripe configuration options.</summary>
    private readonly IOptions<StripeOptions> _options;

    /// <summary>Builds the Stripe API client.</summary>
    private readonly StripeClientProvider _client;

    /// <summary>The clock the look-up window is measured on.</summary>
    private readonly IClock _clock;

    /// <summary>Logger instance.</summary>
    private readonly ILogger<StripeStoredCredentialPlugin> _logger;

    /// <summary>Initializes the capability.</summary>
    /// <param name="options">Stripe configuration options.</param>
    /// <param name="client">Builds the Stripe API client.</param>
    /// <param name="clock">The clock the look-up window is measured on.</param>
    /// <param name="logger">Logger instance.</param>
    public StripeStoredCredentialPlugin(
        IOptions<StripeOptions> options, StripeClientProvider client, IClock clock, ILogger<StripeStoredCredentialPlugin> logger)
    {
        _options = options;
        _client = client;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Gets whether the deployment carries the Stripe credentials; without them the capability is not offered.</summary>
    public bool IsConfigured => _options.Value.IsConfigured;

    /// <inheritdoc />
    /// <remarks>
    /// Looks for a customer already created for the client reference first, then creates one under an idempotency
    /// key. The search index lags a created customer by up to about a minute, which is why the creation also carries
    /// a key (kept 24 hours): together they cover two parallel requests and a crash between creating a customer and storing it.
    /// </remarks>
    public async Task<CredentialCustomerResult> EnsureCustomerAsync(CredentialCustomerContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var service = new CustomerService(_client.Client);
        var found = await service.SearchAsync(
            new CustomerSearchOptions { Query = $"metadata['{CustomerClientMetadataKey}']:'{EscapeQuery(context.ClientReference)}'", Limit = 1 },
            cancellationToken: ct);
        if (found.Data.FirstOrDefault() is { } existing)
        {
            return new CredentialCustomerResult(existing.Id);
        }

        var customer = await service.CreateAsync(
            new CustomerCreateOptions
            {
                Name = string.IsNullOrWhiteSpace(context.Name) ? null : context.Name,
                Email = string.IsNullOrWhiteSpace(context.Email) ? null : context.Email,
                Metadata = new Dictionary<string, string> { [CustomerClientMetadataKey] = context.ClientReference },
            },
            new RequestOptions { IdempotencyKey = $"customer-create-client-{context.ClientReference}" },
            ct);
        _logger.LogInformation("Created Stripe customer {CustomerId} for client {ClientReference}", customer.Id, context.ClientReference);
        return new CredentialCustomerResult(customer.Id);
    }

    /// <inheritdoc />
    public async Task<SaveMethodSession> BeginSaveMethodAsync(SaveMethodContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var service = new SetupIntentService(_client.Client);
        var intent = await service.CreateAsync(
            new SetupIntentCreateOptions
            {
                Customer = context.CustomerRef,
                PaymentMethodTypes = [CardMethodType],
                Usage = OffSessionUsage,
            },
            new RequestOptions { IdempotencyKey = $"setup-{context.CustomerRef}-{Guid.NewGuid():N}" },
            ct);
        _logger.LogInformation("Created Stripe SetupIntent {SetupIntentId} for customer {CustomerId}", intent.Id, context.CustomerRef);

        // The card form is Stripe's own, mounted in the browser with the client secret; the return address is for
        // gateways that send the payer away and is not used here.
        return new SaveMethodSession(intent.Id, SaveMethodMode.EmbeddedToken, intent.ClientSecret, null);
    }

    /// <inheritdoc />
    public async Task<SavedMethodResult> CompleteSaveMethodAsync(string sessionId, CancellationToken ct)
    {
        var intent = await new SetupIntentService(_client.Client).GetAsync(sessionId, cancellationToken: ct);
        if (intent.Status != SucceededStatus || string.IsNullOrEmpty(intent.PaymentMethodId))
        {
            // An abandoned or failed session is a normal answer, not an error.
            return new SavedMethodResult(false, null);
        }

        var customer = await new CustomerService(_client.Client).GetAsync(intent.CustomerId, cancellationToken: ct);
        var method = await new PaymentMethodService(_client.Client).GetAsync(intent.PaymentMethodId, cancellationToken: ct);
        return new SavedMethodResult(true, ToSavedMethod(method, customer.InvoiceSettings?.DefaultPaymentMethodId));
    }

    /// <inheritdoc />
    public async Task<StoredChargeResult> ChargeAsync(StoredChargeInstruction instruction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        var service = new PaymentIntentService(_client.Client);
        try
        {
            var intent = await service.CreateAsync(
                new PaymentIntentCreateOptions
                {
                    Amount = ToStripeAmount(instruction),
                    Currency = instruction.Currency.ToLowerInvariant(),
                    Customer = instruction.CustomerRef,
                    PaymentMethod = instruction.MethodRef,
                    Confirm = true,
                    OffSession = true,
                    Metadata = new Dictionary<string, string>
                    {
                        [InvoiceReferenceMetadataKey] = instruction.InvoiceReference,
                        [IdempotencyMetadataKey] = instruction.IdempotencyKey,
                    },
                },
                new RequestOptions { IdempotencyKey = instruction.IdempotencyKey },
                ct);
            return ToResult(intent);
        }
        catch (StripeException ex) when (ex.StripeError?.Type == CardErrorType)
        {
            // A card Stripe refused. The cardholder can fix a card that needs authentication; a decline is final.
            _logger.LogWarning("Stripe refused an off-session charge for customer {CustomerId}: {Code}", instruction.CustomerRef, ex.StripeError.Code);
            return ex.StripeError.Code == AuthenticationRequiredCode
                ? new StoredChargeResult(StoredChargeOutcome.ActionRequired, null, null, null, null)
                : new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads the customer's payment intents of the look-up window rather than repeating the charge: a repeated
    /// create under a key Stripe never saw would take the money anew. The intent is matched on the idempotency
    /// key every charge carries in its metadata. The search API is not used, because it lags and a "not found"
    /// from a lagging index would be trusted.
    /// </remarks>
    public async Task<StoredChargeLookup> FindChargeAsync(string customerRef, string idempotencyKey, CancellationToken ct)
    {
        var service = new PaymentIntentService(_client.Client);
        var options = new PaymentIntentListOptions
        {
            Customer = customerRef,
            Created = new DateRangeOptions { GreaterThanOrEqual = (_clock.UtcNow - LookupWindow).UtcDateTime },
        };
        await foreach (var intent in service.ListAutoPagingAsync(options, cancellationToken: ct))
        {
            if (intent.Metadata.TryGetValue(IdempotencyMetadataKey, out var key) && key == idempotencyKey)
            {
                var result = ToResult(intent);
                return new StoredChargeLookup(true, result.Outcome, result.TransactionId, result.AmountMinor, result.Currency, result.ActionUrl);
            }
        }

        return new StoredChargeLookup(false, null, null, null, null, null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SavedMethod>> ListMethodsAsync(string customerRef, CancellationToken ct)
    {
        var customer = await new CustomerService(_client.Client).GetAsync(customerRef, cancellationToken: ct);
        var defaultId = customer.InvoiceSettings?.DefaultPaymentMethodId;
        var methods = new List<SavedMethod>();
        await foreach (var method in new PaymentMethodService(_client.Client)
                           .ListAutoPagingAsync(new PaymentMethodListOptions { Customer = customerRef, Type = CardMethodType }, cancellationToken: ct)
                           .WithCancellation(ct))
        {
            methods.Add(ToSavedMethod(method, defaultId));
        }

        return methods;
    }

    /// <inheritdoc />
    public async Task SetDefaultMethodAsync(string customerRef, string methodRef, CancellationToken ct)
    {
        await new CustomerService(_client.Client).UpdateAsync(
            customerRef,
            new CustomerUpdateOptions { InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = methodRef } },
            cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task RemoveMethodAsync(string customerRef, string methodRef, CancellationToken ct)
    {
        await new PaymentMethodService(_client.Client).DetachAsync(methodRef, cancellationToken: ct);
    }

    /// <summary>Maps a Stripe payment intent to the outcome of the charge it carries.</summary>
    /// <param name="intent">The payment intent.</param>
    /// <returns>The outcome; a status that took no money and needs nothing from the payer is a decline.</returns>
    private static StoredChargeResult ToResult(PaymentIntent intent)
    {
        switch (intent.Status)
        {
            case SucceededStatus:
                // The charge id is what a refund needs; the intent id stands in when Stripe has not attached one.
                return new StoredChargeResult(
                    StoredChargeOutcome.Succeeded,
                    intent.LatestChargeId ?? intent.Id,
                    StripeAmount.ToMinorUnits(intent.Amount, intent.Currency),
                    intent.Currency.ToUpperInvariant(),
                    null);
            case RequiresActionStatus:
                return new StoredChargeResult(StoredChargeOutcome.ActionRequired, null, null, null, null);
            case ProcessingStatus:
                return new StoredChargeResult(StoredChargeOutcome.Pending, null, null, null, null);
            default:
                // A refused charge leaves its intent at "requires_payment_method" with the reason on it, so a look-up
                // tells a card that needs the cardholder from a plain decline by the recorded error code.
                return intent.LastPaymentError?.Code == AuthenticationRequiredCode
                    ? new StoredChargeResult(StoredChargeOutcome.ActionRequired, null, null, null, null)
                    : new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null);
        }
    }

    /// <summary>Maps a Stripe card to a saved method.</summary>
    /// <param name="method">The Stripe payment method.</param>
    /// <param name="defaultId">The customer's default payment method id.</param>
    /// <returns>The saved method.</returns>
    private static SavedMethod ToSavedMethod(PaymentMethod method, string? defaultId) =>
        new(method.Id, method.Card?.Brand, method.Card?.Last4, (int)(method.Card?.ExpMonth ?? 0), (int)(method.Card?.ExpYear ?? 0), method.Id == defaultId);

    /// <summary>Converts the host's amount in minor units to Stripe's amount for the currency.</summary>
    /// <param name="instruction">The charge.</param>
    /// <returns>The amount Stripe takes.</returns>
    private static long ToStripeAmount(StoredChargeInstruction instruction) =>
        StripeAmount.ToStripe(CurrencyCode.FromMinorUnits(instruction.AmountMinor, instruction.Currency), instruction.Currency);

    /// <summary>Escapes a value for Stripe's search query language, where a quote ends the value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value with quotes and backslashes escaped.</returns>
    private static string EscapeQuery(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
}
