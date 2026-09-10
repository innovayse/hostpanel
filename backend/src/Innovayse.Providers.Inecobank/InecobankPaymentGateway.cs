namespace Innovayse.Providers.Inecobank;

using Innovayse.SDK.Base;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Inecobank (Armenian Card) hosted-payment-page gateway plugin.
/// Configured from the admin Integrations page via <c>integration:inecobank:*</c> settings.
/// </summary>
public sealed class InecobankPaymentGateway : PaymentGatewayBase, IPaymentPlugin
{
    /// <summary>The plugin id as declared in plugin.json.</summary>
    public const string PluginId = "inecobank";

    /// <summary>
    /// The gateway's getOrderStatusExtended.do errorCode meaning "unregistered orderId" —
    /// the session is unknown to the gateway (e.g. it never completed registration or has
    /// expired) and is treated as declined rather than surfaced as an API error.
    /// </summary>
    /// <summary>
    /// Path the API client appends to the configured base URL. Present here only so the
    /// configuration check below and the client that builds the request cannot disagree.
    /// </summary>
    private const string ApiPathSegment = "/payment/rest";

    private const int UnregisteredOrderIdErrorCode = 6;

    /// <summary>
    /// The other code this gateway answers with for an order id it does not know:
    /// <c>{"message":"Wrong order id","code":"9500"}</c>, alongside HTTP 500.
    /// </summary>
    /// <remarks>
    /// The manual documents only <see cref="UnregisteredOrderIdErrorCode"/>, but the live
    /// gateway returns this one, and the difference matters in both directions. A reconciler
    /// asking about a session the bank never registered must read "no such order" and settle,
    /// not raise; and the admin panel's connection probe is built on exactly this response --
    /// reaching it proves the credentials were accepted, since a bad password never gets far
    /// enough to be told the order id is wrong.
    /// </remarks>
    private const int WrongOrderIdErrorCode = 9500;

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
        if (status.ErrorCode is UnregisteredOrderIdErrorCode or WrongOrderIdErrorCode)
        {
            return new GatewayPaymentStatus(GatewayPaymentState.Declined, null, $"errorCode:{status.ErrorCode}");
        }

        if (status.ErrorCode != 0)
        {
            throw new InecobankApiException(
                status.ErrorCode, status.ErrorMessage ?? $"getOrderStatusExtended error {status.ErrorCode}");
        }

        // errorCode 0 says only "request processed" — the payment verdict lives in orderStatus.
        return (InecobankOrderStatusCode?)status.OrderStatus switch
        {
            InecobankOrderStatusCode.Deposited => new GatewayPaymentStatus(
                GatewayPaymentState.Paid, status.AuthRefNum ?? gatewayOrderId, "orderStatus:2"),
            InecobankOrderStatusCode.AuthorizationReversed
                or InecobankOrderStatusCode.Refunded
                or InecobankOrderStatusCode.AuthorizationDeclined => new GatewayPaymentStatus(
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
        var baseUrl = Require(InecobankConfigKeys.GatewayUrl);
        var userName = Require(InecobankConfigKeys.Username);
        var password = Require(InecobankConfigKeys.Password);

        // The setting is the origin only; the client appends /payment/rest/<endpoint> itself. An
        // admin copying an endpoint URL out of the merchant manual is the easy mistake here, and
        // left alone it produces a doubled path and a bare "request to the gateway failed" that
        // says nothing about which of the five fields is wrong. Naming it costs one comparison.
        if (baseUrl.Contains(ApiPathSegment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Inecobank gateway URL must be the base address only (e.g. https://pg.inecoecom.am), " +
                $"but '{baseUrl}' already contains '{ApiPathSegment}'.");
        }
        return new InecobankApiClient(_http, new InecobankClientOptions(baseUrl, userName, password), _logger);
    }

    /// <summary>Gets the configured ISO 4217 numeric currency code, defaulting to AMD (051).</summary>
    /// <returns>The numeric currency code.</returns>
    private string Currency() => GetConfig(InecobankConfigKeys.Currency) is { Length: > 0 } c ? c : "051";

    /// <summary>Gets the configured ISO 639-1 payment page language, defaulting to Armenian (hy).</summary>
    /// <returns>The language code.</returns>
    private string Language() => GetConfig(InecobankConfigKeys.Language) is { Length: > 0 } l ? l : "hy";

    /// <summary>Reads a required integration setting, throwing when it is not configured.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns>The setting's value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the setting is missing.</exception>
    private string Require(string key) =>
        GetConfig(key) ?? throw new InvalidOperationException($"Inecobank: '{key}' setting is required.");
}
