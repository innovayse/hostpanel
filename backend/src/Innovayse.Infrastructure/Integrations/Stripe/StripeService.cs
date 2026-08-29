namespace Innovayse.Infrastructure.Integrations.Stripe;

using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Infrastructure.Integrations.Stripe.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using global::Stripe;

/// <summary>
/// Stripe payment service implementation using the Stripe.net SDK.
/// Creates and verifies PaymentIntents for order checkout.
/// </summary>
/// <param name="options">Stripe configuration options.</param>
/// <param name="logger">Logger instance.</param>
public sealed class StripeService(
    IOptions<StripeOptions> options,
    ILogger<StripeService> logger) : IStripeService
{
    /// <summary>
    /// Stripe API client configured with the secret key, built only on first use.
    /// </summary>
    /// <remarks>
    /// Built eagerly in the constructor, this threw for every caller that merely depends on
    /// <see cref="IStripeService"/> the moment a deployment has no Stripe secret key configured
    /// -- including a payment-methods lookup for an account that has never attached a card and
    /// was never going to call Stripe at all. Deferred construction means a missing key only
    /// breaks the calls that actually need Stripe.
    /// </remarks>
    private readonly Lazy<StripeClient> _client = new(() => new StripeClient(options.Value.SecretKey));

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
        var service = new PaymentIntentService(_client.Value);
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
        var service = new PaymentIntentService(_client.Value);
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

    /// <inheritdoc />
    public async Task<string> RefundAsync(string transactionId, CancellationToken ct)
    {
        var service = new RefundService(_client.Value);

        // Stripe accepts either a charge ID (ch_xxx) or a payment intent ID (pi_xxx).
        var options = transactionId.StartsWith("ch_", StringComparison.OrdinalIgnoreCase)
            ? new RefundCreateOptions { Charge = transactionId }
            : new RefundCreateOptions { PaymentIntent = transactionId };

        var refund = await service.CreateAsync(options, cancellationToken: ct);

        logger.LogInformation(
            "Issued Stripe refund {RefundId} for transaction {TransactionId}",
            refund.Id, transactionId);

        return refund.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StripePaymentMethodDto>> ListPaymentMethodsAsync(
        string customerId, CancellationToken ct)
    {
        var customerService = new CustomerService(_client.Value);
        var customer = await customerService.GetAsync(customerId, cancellationToken: ct);
        var defaultId = customer.InvoiceSettings?.DefaultPaymentMethodId;

        var methodService = new PaymentMethodService(_client.Value);
        var result = new List<StripePaymentMethodDto>();
        await foreach (var m in methodService.ListAutoPagingAsync(
            new PaymentMethodListOptions { Customer = customerId }, cancellationToken: ct)
            .WithCancellation(ct))
        {
            result.Add(new StripePaymentMethodDto(
                m.Id,
                m.Type,
                m.Card?.Brand ?? m.UsBankAccount?.BankName,
                m.Card?.Last4 ?? m.UsBankAccount?.Last4,
                (int?)m.Card?.ExpMonth,
                (int?)m.Card?.ExpYear,
                m.Id == defaultId));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SetDefaultPaymentMethodAsync(
        string customerId, string paymentMethodId, CancellationToken ct)
    {
        var service = new CustomerService(_client.Value);
        await service.UpdateAsync(customerId, new CustomerUpdateOptions
        {
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                DefaultPaymentMethod = paymentMethodId,
            },
        }, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task DetachPaymentMethodAsync(string paymentMethodId, CancellationToken ct)
    {
        var service = new PaymentMethodService(_client.Value);
        await service.DetachAsync(paymentMethodId, cancellationToken: ct);
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
