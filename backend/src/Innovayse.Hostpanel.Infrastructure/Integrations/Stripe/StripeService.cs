using System.Globalization;
using System.Net;
using global::Stripe;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;
/// <summary>
/// Stripe payment service implementation using the Stripe.net SDK.
/// Creates and verifies PaymentIntents for order checkout.
/// </summary>
public sealed class StripeService : ICardPaymentGateway
{
    /// <summary>Stripe configuration options.</summary>
    private readonly IOptions<StripeOptions> _options;

    /// <summary>Logger instance.</summary>
    private readonly ILogger<StripeService> _logger;

    /// <summary>Initializes a new instance of the <see cref="StripeService"/> class.</summary>
    /// <param name="options">Stripe configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public StripeService(IOptions<StripeOptions> options, ILogger<StripeService> logger)
    {
        _options = options;
        _logger = logger;
        _client = new Lazy<StripeClient>(() => new StripeClient(new StripeClientOptions
        {
            ApiKey = _options.Value.SecretKey,
            ApiBase = _options.Value.ApiBaseUrl.Length == 0 ? null : _options.Value.ApiBaseUrl,
        }));
    }

    /// <summary>The payment-intent status Stripe reports once the charge has gone through.</summary>
    private const string SucceededStatus = "succeeded";

    /// <summary>The metadata key that ties a Stripe payment back to its order.</summary>
    private const string OrderIdMetadataKey = "orderId";

    /// <summary>The metadata key that ties a Stripe payment back to its invoice.</summary>
    private const string InvoiceIdMetadataKey = "invoiceId";

    /// <summary>The metadata key that ties a saved-method charge to its payment attempt (its idempotency key).</summary>
    private const string AttemptMetadataKey = "attemptKey";

    /// <summary>The metadata key that ties a Stripe payment back to its client.</summary>
    private const string ClientIdMetadataKey = "clientId";

    /// <summary>The error type Stripe reports for a card that was declined.</summary>
    private const string CardErrorType = "card_error";

    /// <summary>The payment method type Stripe reports for a card.</summary>
    private const string CardMethodType = "card";

    /// <summary>The payment method type Stripe reports for a US bank account.</summary>
    private const string BankAccountMethodType = "us_bank_account";

    /// <inheritdoc />
    public bool IsConfigured => _options.Value.IsConfigured;

    /// <summary>
    /// Stripe API client configured with the secret key, built only on first use.
    /// </summary>
    /// <remarks>
    /// Never built when <see cref="IsConfigured"/> is false: every member answers
    /// <see cref="CardGatewayFailure.NotConfigured"/> before it touches this.
    /// <para>
    /// Built eagerly in the constructor, this threw for every caller that merely depends on
    /// <see cref="ICardPaymentGateway"/> the moment a deployment has no Stripe secret key configured,
    /// including a payment-methods lookup for an account that has never attached a card and
    /// was never going to call Stripe at all. Deferred construction means a missing key only
    /// breaks the calls that actually need Stripe.
    /// </para>
    /// </remarks>
    private readonly Lazy<StripeClient> _client;

    /// <inheritdoc />
    public async Task<CardPaymentIntentResult> CreatePaymentIntentAsync(
        CardPaymentIntentInstruction instruction,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new CardPaymentIntentResult(null, CardGatewayFailure.NotConfigured);
        }

        var service = new PaymentIntentService(_client.Value);
        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = StripeAmount.ToStripe(instruction.Amount, instruction.Currency),
            Currency = instruction.Currency.ToLowerInvariant(),
            Metadata = new Dictionary<string, string>
            {
                [OrderIdMetadataKey] = instruction.OrderId.ToString(CultureInfo.InvariantCulture),
                [InvoiceIdMetadataKey] = instruction.InvoiceId.ToString(CultureInfo.InvariantCulture),
                [ClientIdMetadataKey] = instruction.ClientId.ToString(CultureInfo.InvariantCulture),
            },
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
            },
        };

        PaymentIntent intent;
        try
        {
            intent = await service.CreateAsync(
                createOptions, new RequestOptions { IdempotencyKey = instruction.IdempotencyKey }, ct);
        }
        catch (Exception ex) when (IsGatewayFailure(ex, ct))
        {
            var failure = ToFailure(ex);
            _logger.LogWarning(ex, "Stripe could not open a PaymentIntent for invoice {InvoiceId}: {Failure}", instruction.InvoiceId, failure);
            return new CardPaymentIntentResult(null, failure);
        }

        _logger.LogInformation(
            "Created Stripe PaymentIntent {PaymentIntentId} for {Amount} {Currency}",
            intent.Id, instruction.Amount, instruction.Currency);

        return new CardPaymentIntentResult(intent.ClientSecret);
    }

    /// <inheritdoc />
    public async Task<CardPaymentIntentStatus> VerifyPaymentIntentAsync(
        CardPaymentVerifyInstruction instruction,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new CardPaymentIntentStatus(false, null, 0, string.Empty, null, null, CardGatewayFailure.NotConfigured);
        }

        var paymentIntentId = instruction.PaymentIntentId;
        var service = new PaymentIntentService(_client.Value);
        PaymentIntent intent;
        try
        {
            intent = await service.GetAsync(paymentIntentId, cancellationToken: ct);
        }
        catch (Exception ex) when (IsGatewayFailure(ex, ct))
        {
            var failure = ToFailure(ex);
            _logger.LogWarning(ex, "Stripe could not read PaymentIntent {PaymentIntentId}: {Failure}", paymentIntentId, failure);
            return new CardPaymentIntentStatus(false, null, 0, string.Empty, null, null, failure);
        }

        var succeeded = intent.Status == SucceededStatus;

        if (succeeded)
        {
            _logger.LogInformation(
                "PaymentIntent {PaymentIntentId} succeeded with transaction {TransactionId}",
                paymentIntentId, intent.LatestChargeId ?? intent.Id);
        }
        else
        {
            _logger.LogWarning(
                "PaymentIntent {PaymentIntentId} has status {Status}, expected succeeded",
                paymentIntentId, intent.Status);
        }

        return new CardPaymentIntentStatus(
            succeeded,
            succeeded ? intent.LatestChargeId ?? intent.Id : null,
            StripeAmount.ToMinorUnits(intent.Amount, intent.Currency),
            intent.Currency,
            ReadMetadataId(intent.Metadata, OrderIdMetadataKey),
            ReadMetadataId(intent.Metadata, InvoiceIdMetadataKey));
    }

    /// <inheritdoc />
    public async Task<CardChargeResult> ChargeSavedPaymentMethodAsync(
        CardChargeInstruction instruction,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new CardChargeResult(false, null, CardGatewayFailure.NotConfigured);
        }

        var service = new PaymentIntentService(_client.Value);
        try
        {
            var intent = await service.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = StripeAmount.ToStripe(instruction.Amount, instruction.Currency),
                Currency = instruction.Currency.ToLowerInvariant(),
                Customer = instruction.CustomerId,
                PaymentMethod = instruction.PaymentMethodId,
                Confirm = true,
                OffSession = true,
                Metadata = new Dictionary<string, string>
                {
                    [InvoiceIdMetadataKey] = instruction.InvoiceId.ToString(CultureInfo.InvariantCulture),
                    [AttemptMetadataKey] = instruction.IdempotencyKey,
                },
            }, new RequestOptions { IdempotencyKey = instruction.IdempotencyKey }, ct);

            // Anything but "succeeded" (for instance a card that needs the cardholder to authenticate)
            // has taken no money, so it must never read as paid.
            if (intent.Status == SucceededStatus)
            {
                return new CardChargeResult(true, intent.LatestChargeId ?? intent.Id);
            }

            _logger.LogWarning(
                "Off-session PaymentIntent {PaymentIntentId} has status {Status}, expected succeeded",
                intent.Id, intent.Status);
            return new CardChargeResult(false, null);
        }
        catch (StripeException ex) when (ex.StripeError?.Type == CardErrorType)
        {
            _logger.LogWarning(
                "Stripe declined an off-session charge for customer {CustomerId}: {Code}",
                instruction.CustomerId, ex.StripeError.Code);
            return new CardChargeResult(false, null);
        }
        catch (Exception ex) when (IsGatewayFailure(ex, ct))
        {
            // Retrying the same attempt is safe: its idempotency key keeps it to one charge.
            var failure = ToFailure(ex);
            _logger.LogWarning(ex, "Stripe failed an off-session charge for invoice {InvoiceId}: {Failure}", instruction.InvoiceId, failure);
            return new CardChargeResult(false, null, failure);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads the customer's payment intents rather than repeating the charge: a repeated create
    /// under a key Stripe never saw would take the money anew. The intent is matched on the invoice
    /// and attempt metadata every saved-method charge carries.
    /// </remarks>
    public async Task<CardChargeLookupResult> FindChargeAsync(CardChargeLookupInstruction instruction, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return CardChargeLookupResult.Failed(CardGatewayFailure.NotConfigured);
        }

        var service = new PaymentIntentService(_client.Value);
        var invoiceId = instruction.InvoiceId.ToString(CultureInfo.InvariantCulture);
        var options = new PaymentIntentListOptions { Customer = instruction.CustomerId };
        try
        {
            await foreach (var intent in service.ListAutoPagingAsync(options, cancellationToken: ct))
            {
                if (intent.Status == SucceededStatus
                    && intent.Metadata.TryGetValue(InvoiceIdMetadataKey, out var invoice) && invoice == invoiceId
                    && intent.Metadata.TryGetValue(AttemptMetadataKey, out var attempt) && attempt == instruction.IdempotencyKey)
                {
                    return new CardChargeLookupResult(
                        true,
                        intent.LatestChargeId ?? intent.Id,
                        StripeAmount.FromStripe(intent.Amount, intent.Currency),
                        intent.Currency);
                }
            }
        }
        catch (Exception ex) when (IsGatewayFailure(ex, ct))
        {
            var failure = ToFailure(ex);
            _logger.LogWarning(ex, "Stripe could not look up the charge for invoice {InvoiceId}: {Failure}", instruction.InvoiceId, failure);
            return CardChargeLookupResult.Failed(failure);
        }

        return CardChargeLookupResult.None;
    }

    /// <inheritdoc />
    public async Task<CardRefundResult> RefundAsync(CardRefundInstruction instruction, CancellationToken ct)
    {
        // A refund has no failure kind of its own; without a key nothing can be refunded, so it is refused.
        if (!IsConfigured)
        {
            return new CardRefundResult(false, null);
        }

        var transactionId = instruction.TransactionId;
        var service = new RefundService(_client.Value);

        // Stripe accepts either a charge ID (ch_xxx) or a payment intent ID (pi_xxx).
        var refundOptions = transactionId.StartsWith("ch_", StringComparison.OrdinalIgnoreCase)
            ? new RefundCreateOptions { Charge = transactionId }
            : new RefundCreateOptions { PaymentIntent = transactionId };
        refundOptions.Amount = StripeAmount.ToStripe(instruction.Amount, instruction.Currency);

        try
        {
            var refund = await service.CreateAsync(
                refundOptions, new RequestOptions { IdempotencyKey = instruction.IdempotencyKey }, ct);

            _logger.LogInformation(
                "Issued Stripe refund {RefundId} for transaction {TransactionId}",
                refund.Id, transactionId);

            return new CardRefundResult(true, refund.Id);
        }
        catch (StripeException ex) when (IsRefusal(ex))
        {
            // A refusing 4xx is the gateway declining this refund (already refunded, more than the
            // charge holds); an outage, a 5xx, a 429 or a 409 (key in use) still throws, because
            // nothing is known about the money then.
            _logger.LogWarning(
                "Stripe refused a refund for transaction {TransactionId}: {Code}",
                transactionId, ex.StripeError?.Code);
            return new CardRefundResult(false, null);
        }
    }

    /// <inheritdoc />
    public async Task<CardPaymentMethodsResult> ListPaymentMethodsAsync(
        CardCustomerInstruction instruction, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return CardPaymentMethodsResult.Failed(CardGatewayFailure.NotConfigured);
        }

        var customerId = instruction.CustomerId;
        try
        {
            var customerService = new CustomerService(_client.Value);
            var customer = await customerService.GetAsync(customerId, cancellationToken: ct);
            var defaultId = customer.InvoiceSettings?.DefaultPaymentMethodId;

            var methodService = new PaymentMethodService(_client.Value);
            var result = new List<CardPaymentMethodDto>();
            await foreach (var m in methodService.ListAutoPagingAsync(
                new PaymentMethodListOptions { Customer = customerId }, cancellationToken: ct)
                .WithCancellation(ct))
            {
                result.Add(new CardPaymentMethodDto(
                    m.Id,
                    ToSavedPaymentMethodType(m.Type),
                    m.Card?.Brand ?? m.UsBankAccount?.BankName,
                    m.Card?.Last4 ?? m.UsBankAccount?.Last4,
                    (int?)m.Card?.ExpMonth,
                    (int?)m.Card?.ExpYear,
                    m.Id == defaultId));
            }

            return new CardPaymentMethodsResult(result);
        }
        catch (Exception ex) when (IsGatewayFailure(ex, ct))
        {
            var failure = ToFailure(ex);
            _logger.LogWarning(ex, "Stripe could not list the payment methods of customer {CustomerId}: {Failure}", customerId, failure);
            return CardPaymentMethodsResult.Failed(failure);
        }
    }

    /// <inheritdoc />
    public async Task<CardGatewayOperationResult> SetDefaultPaymentMethodAsync(
        CardDefaultMethodInstruction instruction, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new CardGatewayOperationResult(CardGatewayFailure.NotConfigured);
        }

        var service = new CustomerService(_client.Value);
        try
        {
            await service.UpdateAsync(instruction.CustomerId, new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = instruction.PaymentMethodId,
                },
            }, cancellationToken: ct);
            return CardGatewayOperationResult.Succeeded;
        }
        catch (Exception ex) when (IsGatewayFailure(ex, ct))
        {
            var failure = ToFailure(ex);
            _logger.LogWarning(ex, "Stripe could not set the default payment method of customer {CustomerId}: {Failure}", instruction.CustomerId, failure);
            return new CardGatewayOperationResult(failure);
        }
    }

    /// <inheritdoc />
    public async Task<CardGatewayOperationResult> DetachPaymentMethodAsync(CardDetachInstruction instruction, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new CardGatewayOperationResult(CardGatewayFailure.NotConfigured);
        }

        var service = new PaymentMethodService(_client.Value);
        try
        {
            await service.DetachAsync(instruction.PaymentMethodId, cancellationToken: ct);
            return CardGatewayOperationResult.Succeeded;
        }
        catch (Exception ex) when (IsGatewayFailure(ex, ct))
        {
            var failure = ToFailure(ex);
            _logger.LogWarning(ex, "Stripe could not detach payment method {PaymentMethodId}: {Failure}", instruction.PaymentMethodId, failure);
            return new CardGatewayOperationResult(failure);
        }
    }

    /// <summary>Maps Stripe's payment method type code to the kind the application knows.</summary>
    /// <param name="type">Stripe's type code (e.g. <c>card</c>).</param>
    /// <returns>The matching kind, or <see cref="SavedPaymentMethodType.Other"/> for any code without one.</returns>
    private static SavedPaymentMethodType ToSavedPaymentMethodType(string type) => type switch
    {
        CardMethodType => SavedPaymentMethodType.Card,
        BankAccountMethodType => SavedPaymentMethodType.BankAccount,
        _ => SavedPaymentMethodType.Other,
    };

    /// <summary>
    /// Whether an exception is a failure of the gateway call itself (an error Stripe answered with, a
    /// network error, or a timeout) rather than the caller cancelling or a bug in this code.
    /// </summary>
    /// <param name="ex">The exception the call threw.</param>
    /// <param name="ct">The caller's cancellation token; a cancellation it asked for is never a gateway failure.</param>
    /// <returns><see langword="true"/> when the call failed at or on the way to the gateway.</returns>
    private static bool IsGatewayFailure(Exception ex, CancellationToken ct) => ex switch
    {
        StripeException or HttpRequestException => true,
        OperationCanceledException => !ct.IsCancellationRequested,
        _ => false,
    };

    /// <summary>Translates a gateway call failure to the application's own failure kind.</summary>
    /// <param name="ex">An exception <see cref="IsGatewayFailure"/> accepted.</param>
    /// <returns>
    /// <see cref="CardGatewayFailure.Refused"/> for a 4xx that refuses the request; otherwise (a 5xx,
    /// a 429, a 409, a network error, a timeout) <see cref="CardGatewayFailure.Unavailable"/>, since
    /// the outcome at Stripe is then unknown.
    /// </returns>
    private static CardGatewayFailure ToFailure(Exception ex) =>
        IsRefusal(ex) ? CardGatewayFailure.Refused : CardGatewayFailure.Unavailable;

    /// <summary>
    /// Whether an exception is Stripe refusing the request: a 4xx other than 429 (rate limited) and
    /// 409 (idempotency key in use), which say nothing about whether the request was carried out.
    /// </summary>
    /// <param name="ex">The exception the call threw.</param>
    /// <returns><see langword="true"/> when Stripe definitively refused the request.</returns>
    private static bool IsRefusal(Exception ex) =>
        ex is StripeException { HttpStatusCode: var status }
        && (int)status is >= 400 and < 500
        && status is not (HttpStatusCode.TooManyRequests or HttpStatusCode.Conflict);

    /// <summary>Reads an integer id the backend stored in an intent's metadata.</summary>
    /// <param name="metadata">The intent's metadata, or <see langword="null"/>.</param>
    /// <param name="key">The metadata key.</param>
    /// <returns>The id, or <see langword="null"/> when the key is missing or not an integer.</returns>
    private static int? ReadMetadataId(Dictionary<string, string>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}
