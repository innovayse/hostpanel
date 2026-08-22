namespace Innovayse.Providers.Inecobank;

using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>Input for <see cref="InecobankApiClient.RegisterOrderAsync"/>.</summary>
/// <param name="OrderNumber">Merchant order number, unique per attempt, ≤ 25 chars.</param>
/// <param name="AmountMinor">Amount in minor currency units (luma).</param>
/// <param name="Currency">ISO 4217 numeric currency code — always sent explicitly (gateway defaults to 643/RUB).</param>
/// <param name="ReturnUrl">Absolute URL the payer is redirected back to.</param>
/// <param name="Description">Optional description; sanitized to the gateway's rules before sending.</param>
/// <param name="Language">Optional ISO 639-1 payment page language.</param>
public sealed record InecobankRegisterRequest(
    string OrderNumber, long AmountMinor, string Currency,
    string ReturnUrl, string? Description, string? Language);

/// <summary>Successful register.do response.</summary>
/// <param name="OrderId">Gateway-side order id.</param>
/// <param name="FormUrl">Hosted payment page URL.</param>
public sealed record InecobankRegisterResult(string OrderId, string FormUrl);

/// <summary>Parsed getOrderStatusExtended.do response.</summary>
/// <param name="ErrorCode">Gateway errorCode (0 = request processed; does NOT mean paid).</param>
/// <param name="OrderStatus">Gateway orderStatus (2 = deposited); null when absent.</param>
/// <param name="ErrorMessage">Gateway errorMessage when present.</param>
/// <param name="AuthRefNum">Bank reference number when present (used as the transaction id).</param>
public sealed record InecobankOrderStatus(
    int ErrorCode, int? OrderStatus, string? ErrorMessage, string? AuthRefNum);

/// <summary>
/// Low-level HTTP client for the Inecobank (Armenian Card) merchant REST API.
/// Sends form-urlencoded POSTs and parses the JSON responses leniently — the
/// gateway returns errorCode sometimes as a number and sometimes as a string.
/// </summary>
public sealed class InecobankApiClient
{
    private static readonly char[] ForbiddenDescriptionChars = ['%', '+', '\r', '\n'];

    private readonly HttpClient _http;
    private readonly InecobankClientOptions _options;
    private readonly ILogger _logger;

    /// <summary>Initializes the client.</summary>
    /// <param name="http">HTTP client used for all requests.</param>
    /// <param name="options">Gateway URL and merchant credentials.</param>
    /// <param name="logger">Structured logger; credentials are never logged.</param>
    public InecobankApiClient(HttpClient http, InecobankClientOptions options, ILogger logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    /// <summary>Registers an order (one-stage payment) and returns the redirect session.</summary>
    /// <param name="request">The registration parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gateway order id and payment form URL.</returns>
    /// <exception cref="InecobankApiException">Thrown on a non-zero errorCode or a malformed response.</exception>
    public async Task<InecobankRegisterResult> RegisterOrderAsync(
        InecobankRegisterRequest request, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["userName"] = _options.UserName,
            ["password"] = _options.Password,
            ["orderNumber"] = request.OrderNumber,
            ["amount"] = request.AmountMinor.ToString(),
            ["currency"] = request.Currency,
            ["returnUrl"] = request.ReturnUrl,
        };

        var description = SanitizeDescription(request.Description);
        if (!string.IsNullOrEmpty(description))
        {
            fields["description"] = description;
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            fields["language"] = request.Language;
        }

        using var doc = await PostAsync("register.do", fields, ct);
        ThrowOnError(doc, "register.do");

        var orderId = GetString(doc, "orderId");
        var formUrl = GetString(doc, "formUrl");
        if (orderId is null || formUrl is null)
        {
            throw new InecobankApiException(-1, "register.do returned no orderId/formUrl.");
        }

        _logger.LogInformation(
            "Inecobank order registered: merchant orderNumber {OrderNumber} -> gateway order {GatewayOrderId}",
            request.OrderNumber, orderId);
        return new InecobankRegisterResult(orderId, formUrl);
    }

    /// <summary>Fetches the extended status of a gateway order.</summary>
    /// <param name="gatewayOrderId">The gateway-side order id.</param>
    /// <param name="language">Optional ISO 639-1 language for error messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed status; never throws on business error codes — callers map them.</returns>
    public async Task<InecobankOrderStatus> GetOrderStatusAsync(
        string gatewayOrderId, string? language, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["userName"] = _options.UserName,
            ["password"] = _options.Password,
            ["orderId"] = gatewayOrderId,
        };
        if (!string.IsNullOrEmpty(language))
        {
            fields["language"] = language;
        }

        using var doc = await PostAsync("getOrderStatusExtended.do", fields, ct);
        var root = doc.RootElement;
        return new InecobankOrderStatus(
            ErrorCode: GetLenientInt(root, "errorCode") ?? 0,
            OrderStatus: GetLenientInt(root, "orderStatus"),
            ErrorMessage: GetString(doc, "errorMessage"),
            AuthRefNum: GetString(doc, "authRefNum"));
    }

    /// <summary>Refunds a deposited order, fully or partially.</summary>
    /// <param name="gatewayOrderId">The gateway-side order id.</param>
    /// <param name="amountMinor">Refund amount in minor units.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InecobankApiException">Thrown on a non-zero errorCode.</exception>
    public async Task RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["userName"] = _options.UserName,
            ["password"] = _options.Password,
            ["orderId"] = gatewayOrderId,
            ["amount"] = amountMinor.ToString(),
        };

        using var doc = await PostAsync("refund.do", fields, ct);
        ThrowOnError(doc, "refund.do");
        _logger.LogInformation(
            "Inecobank refund accepted for gateway order {GatewayOrderId}, amount {AmountMinor}",
            gatewayOrderId, amountMinor);
    }

    /// <summary>Removes characters the gateway forbids and truncates to 99 chars.</summary>
    /// <param name="description">The raw description, possibly null.</param>
    /// <returns>The sanitized description, or null.</returns>
    internal static string? SanitizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var cleaned = string.Concat(description.Where(c => !ForbiddenDescriptionChars.Contains(c)));
        return cleaned.Length <= 99 ? cleaned : cleaned[..99];
    }

    private async Task<JsonDocument> PostAsync(
        string endpoint, Dictionary<string, string> fields, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/payment/rest/{endpoint}";
        using var content = new FormUrlEncodedContent(fields);
        using var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(json);
    }

    private static void ThrowOnError(JsonDocument doc, string endpoint)
    {
        var code = GetLenientInt(doc.RootElement, "errorCode") ?? 0;
        if (code != 0)
        {
            var message = GetString(doc, "errorMessage") ?? $"Gateway error {code}";
            throw new InecobankApiException(code, $"{endpoint}: {message}");
        }
    }

    private static string? GetString(JsonDocument doc, string property) =>
        doc.RootElement.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int? GetLenientInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetInt32(),
            JsonValueKind.String when int.TryParse(el.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }
}
