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

    private readonly ILogger<InecobankPaymentGateway> _logger;
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

        // errorCode 6 = "unregistered orderId": the session is unknown to the gateway — treat as declined.
        if (status.ErrorCode == 6)
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

    private InecobankApiClient CreateClient()
    {
        var baseUrl = Require("gateway_url");
        var userName = Require("username");
        var password = Require("password");
        return new InecobankApiClient(_http, new InecobankClientOptions(baseUrl, userName, password), _logger);
    }

    private string Currency() => GetConfig("currency") is { Length: > 0 } c ? c : "051";

    private string Language() => GetConfig("language") is { Length: > 0 } l ? l : "hy";

    private string Require(string key) =>
        GetConfig(key) ?? throw new InvalidOperationException($"Inecobank: '{key}' setting is required.");
}
