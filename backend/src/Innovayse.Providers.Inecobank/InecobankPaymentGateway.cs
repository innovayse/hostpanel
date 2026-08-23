namespace Innovayse.Providers.Inecobank;

using Innovayse.SDK.Base;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Inecobank (Armenian Card) hosted-payment-page gateway plugin.
/// Configured from the admin Integrations page via <c>integration:innovayse-inecobank:*</c> settings.
/// </summary>
public sealed class InecobankPaymentGateway : PaymentGatewayBase, IPaymentPlugin
{
    /// <summary>The plugin id as declared in plugin.json.</summary>
    public const string PluginId = "innovayse-inecobank";

    /// <summary>
    /// The gateway's getOrderStatusExtended.do errorCode meaning "unregistered orderId" —
    /// the session is unknown to the gateway (e.g. it never completed registration or has
    /// expired) and is treated as declined rather than surfaced as an API error.
    /// </summary>
    private const int UnregisteredOrderIdErrorCode = 6;

    /// <summary>Structured logger, also passed through to <see cref="InecobankApiClient"/>.</summary>
    private readonly ILogger<InecobankPaymentGateway> _logger;

    /// <summary>HTTP client used for all gateway API calls.</summary>
    private readonly HttpClient _http;

    /// <summary>Initializes the gateway; called by the plugin resolver via ActivatorUtilities.</summary>
    /// <param name="configuration">Composed configuration carrying the integration settings.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="httpClientFactory">Factory for the outbound HTTP client.</param>
    public InecobankPaymentGateway(
        IConfiguration configuration,
        ILogger<InecobankPaymentGateway> logger,
        IHttpClientFactory httpClientFactory)
        : this(configuration, logger, httpClientFactory.CreateClient(PluginId))
    {
    }

    /// <summary>
    /// Test seam: initializes the gateway with an explicit HTTP client.
    /// Internal on purpose — ActivatorUtilities must only ever see the factory ctor
    /// (an ambient HttpClient is not registered in DI and would fail resolution).
    /// </summary>
    /// <param name="configuration">Composed configuration carrying the integration settings.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="httpClient">HTTP client used for gateway calls.</param>
    internal InecobankPaymentGateway(
        IConfiguration configuration,
        ILogger<InecobankPaymentGateway> logger,
        HttpClient httpClient)
        : base(PluginId, configuration, logger)
    {
        _logger = logger;
        _http = httpClient;
    }

    /// <inheritdoc/>
    public string CurrencyCode => Currency();

    /// <inheritdoc/>
    public async Task<PaymentSession> CreatePaymentAsync(PaymentRequest request, CancellationToken ct)
    {
        var client = CreateClient();
        var result = await client.RegisterOrderAsync(
            new InecobankRegisterRequest(
                request.OrderNumber,
                request.AmountMinor,
                Currency(),
                request.ReturnUrl,
                request.Description,
                request.Language ?? Language()),
            ct);
        return new PaymentSession(result.OrderId, result.FormUrl);
    }

    /// <inheritdoc/>
    public async Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
    {
        var status = await CreateClient().GetOrderStatusAsync(gatewayOrderId, Language(), ct);

        // The session is unknown to the gateway — treat as declined.
        if (status.ErrorCode == UnregisteredOrderIdErrorCode)
        {
            return new GatewayPaymentStatus(GatewayPaymentState.Declined, null, $"errorCode:{status.ErrorCode}");
        }

        if (status.ErrorCode != 0)
        {
            throw new InecobankApiException(
                status.ErrorCode, status.ErrorMessage ?? $"getOrderStatusExtended error {status.ErrorCode}");
        }

        // errorCode 0 says only "request processed" — the payment verdict lives in orderStatus.
        return status.OrderStatus switch
        {
            2 => new GatewayPaymentStatus(
                GatewayPaymentState.Paid, status.AuthRefNum ?? gatewayOrderId, "orderStatus:2"),
            3 or 4 or 6 => new GatewayPaymentStatus(
                GatewayPaymentState.Declined, null, $"orderStatus:{status.OrderStatus}"),
            _ => new GatewayPaymentStatus(
                GatewayPaymentState.Pending, null, $"orderStatus:{status.OrderStatus?.ToString() ?? "none"}"),
        };
    }

    /// <inheritdoc/>
    public async Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        // refund.do returns only errorCode — the gateway order id is the only usable reference.
        await CreateClient().RefundAsync(gatewayOrderId, amountMinor, ct);
        return gatewayOrderId;
    }

    /// <summary>Builds a fresh low-level API client from the plugin's current integration settings.</summary>
    /// <returns>A configured <see cref="InecobankApiClient"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a required setting (gateway_url, username, password) is missing.
    /// </exception>
    private InecobankApiClient CreateClient()
    {
        var baseUrl = Require("gateway_url");
        var userName = Require("username");
        var password = Require("password");
        return new InecobankApiClient(_http, new InecobankClientOptions(baseUrl, userName, password), _logger);
    }

    /// <summary>Gets the configured ISO 4217 numeric currency code, defaulting to AMD (051).</summary>
    /// <returns>The numeric currency code.</returns>
    private string Currency() => GetConfig("currency") is { Length: > 0 } c ? c : "051";

    /// <summary>Gets the configured ISO 639-1 payment page language, defaulting to Armenian (hy).</summary>
    /// <returns>The language code.</returns>
    private string Language() => GetConfig("language") is { Length: > 0 } l ? l : "hy";

    /// <summary>Reads a required integration setting, throwing when it is not configured.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns>The setting's value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the setting is missing.</exception>
    private string Require(string key) =>
        GetConfig(key) ?? throw new InvalidOperationException($"Inecobank: '{key}' setting is required.");
}
