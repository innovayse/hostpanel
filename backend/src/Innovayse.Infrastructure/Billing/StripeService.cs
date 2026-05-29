namespace Innovayse.Infrastructure.Billing;

using Innovayse.Application.Billing.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

/// <summary>
/// Stripe payment service implementation using the Stripe.net SDK.
/// Creates and verifies PaymentIntents for order checkout.
/// </summary>
/// <param name="options">Stripe configuration options.</param>
/// <param name="logger">Logger instance.</param>
public sealed class StripeService(
    IOptions<StripeSettings> options,
    ILogger<StripeService> logger) : IStripeService
{
    /// <summary>Stripe API client configured with the secret key.</summary>
    private readonly StripeClient _client = new(options.Value.SecretKey);

    /// <summary>
    /// Zero-decimal currencies that do not need multiplication by 100.
    /// See https://docs.stripe.com/currencies#zero-decimal.
    /// </summary>
    private static readonly HashSet<string> ZeroDecimalCurrencies =
    [
        "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga",
        "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
    ];

    /// <inheritdoc />
    public async Task<string> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        Dictionary<string, string> metadata,
        CancellationToken ct)
    {
        var service = new PaymentIntentService(_client);
        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = ConvertToSmallestUnit(amount, currency),
            Currency = currency.ToLowerInvariant(),
            Metadata = metadata,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
            },
        };

        var intent = await service.CreateAsync(createOptions, cancellationToken: ct);
        logger.LogInformation(
            "Created Stripe PaymentIntent {PaymentIntentId} for {Amount} {Currency}",
            intent.Id, amount, currency);

        return intent.ClientSecret;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? TransactionId)> VerifyPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken ct)
    {
        var service = new PaymentIntentService(_client);
        var intent = await service.GetAsync(paymentIntentId, cancellationToken: ct);

        if (intent.Status == "succeeded")
        {
            var transactionId = intent.LatestChargeId ?? intent.Id;
            logger.LogInformation(
                "PaymentIntent {PaymentIntentId} succeeded with transaction {TransactionId}",
                paymentIntentId, transactionId);
            return (true, transactionId);
        }

        logger.LogWarning(
            "PaymentIntent {PaymentIntentId} has status {Status}, expected succeeded",
            paymentIntentId, intent.Status);
        return (false, null);
    }

    /// <summary>
    /// Converts a decimal amount to the smallest currency unit (e.g. cents for USD).
    /// Zero-decimal currencies like JPY are returned as-is.
    /// </summary>
    /// <param name="amount">The amount in major currency units.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <returns>The amount in the smallest currency unit.</returns>
    private static long ConvertToSmallestUnit(decimal amount, string currency)
    {
        var code = currency.ToLowerInvariant();
        return ZeroDecimalCurrencies.Contains(code)
            ? (long)amount
            : (long)(amount * 100);
    }
}
