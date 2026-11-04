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

    /// <summary>Builds the Stripe API client on first use.</summary>
    private readonly StripeClientProvider _client;

    /// <summary>Initializes a new instance of the <see cref="StripeService"/> class.</summary>
    /// <param name="options">Stripe configuration options.</param>
    /// <param name="client">Builds the Stripe API client on first use.</param>
    /// <param name="logger">Logger instance.</param>
    public StripeService(IOptions<StripeOptions> options, StripeClientProvider client, ILogger<StripeService> logger)
    {
        _options = options;
        _client = client;
        _logger = logger;
    }

    /// <summary>The payment-intent status Stripe reports once the charge has gone through.</summary>
    private const string SucceededStatus = "succeeded";

    /// <summary>The metadata key that ties a Stripe payment back to its order.</summary>
    private const string OrderIdMetadataKey = "orderId";

    /// <summary>The metadata key that ties a Stripe payment back to its invoice.</summary>
    private const string InvoiceIdMetadataKey = "invoiceId";

    /// <summary>The metadata key that ties a Stripe payment back to its client.</summary>
    private const string ClientIdMetadataKey = "clientId";

    /// <summary>The error type Stripe reports for a card that was declined.</summary>
    private const string CardErrorType = "card_error";

    /// <inheritdoc />
    public bool IsConfigured => _options.Value.IsConfigured;

    /// <inheritdoc />
    public async Task<CardPaymentIntentResult> CreatePaymentIntentAsync(
        CardPaymentIntentInstruction instruction,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new CardPaymentIntentResult(null, CardGatewayFailure.NotConfigured);
        }

        var service = new PaymentIntentService(_client.Client);
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
        var service = new PaymentIntentService(_client.Client);
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
    public async Task<CardRefundResult> RefundAsync(CardRefundInstruction instruction, CancellationToken ct)
    {
        // A refund has no failure kind of its own; without a key nothing can be refunded, so it is refused.
        if (!IsConfigured)
        {
            return new CardRefundResult(false, null);
        }

        var transactionId = instruction.TransactionId;
        var service = new RefundService(_client.Client);

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
