namespace Innovayse.Providers.Inecobank;

using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Low-level HTTP client for the Inecobank (Armenian Card) merchant REST API.
/// Sends form-urlencoded POSTs and parses the JSON responses leniently — the
/// gateway returns errorCode sometimes as a number and sometimes as a string.
/// </summary>
public sealed class InecobankApiClient
{
    /// <summary>
    /// Maximum length, in characters, the gateway accepts for the order description field.
    /// Longer descriptions are truncated rather than rejected.
    /// </summary>
    private const int MaxDescriptionLength = 99;

    /// <summary>
    /// Synthetic <see cref="InecobankApiException.ErrorCode"/> used for failures detected on
    /// this side of the HTTP call — a non-2xx response, or a response body that is not valid
    /// JSON — rather than a business error the gateway itself reported. The merchant manual's
    /// errorCode tables are all non-negative (0 = success), so a negative sentinel can never
    /// collide with a real gateway code, no matter which one the bank adds next.
    /// </summary>
    private const int TransportErrorCode = -1;

    /// <summary>
    /// Synthetic <see cref="InecobankApiException.ErrorCode"/> for a gateway that answered
    /// <c>401 Unauthorized</c>. Kept apart from <see cref="TransportErrorCode"/> because the two
    /// need different answers from an operator: a rejected merchant credential is a
    /// configuration mistake to correct, while a transport failure is something to retry.
    /// Verified against the live gateway, which answers <c>401 {"message":"Unauthorized"}</c>
    /// to a wrong password and never uses that status for anything else.
    /// </summary>
    private const int UnauthorizedErrorCode = -2;

    /// <summary>Characters the gateway's request encoding cannot carry and must be stripped from descriptions.</summary>
    private static readonly char[] ForbiddenDescriptionChars = ['%', '+', '\r', '\n'];

    /// <summary>HTTP client used for all requests to the gateway REST API.</summary>
    private readonly HttpClient _http;

    /// <summary>Gateway base URL and merchant credentials.</summary>
    private readonly InecobankClientOptions _options;

    /// <summary>Structured logger; credentials are never logged.</summary>
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

        using var doc = await PostAsync(InecobankEndpoints.Register, fields, ct);
        ThrowOnError(doc, InecobankEndpoints.Register);

        var orderId = GetString(doc, "orderId");
        var formUrl = GetString(doc, "formUrl");
        if (orderId is null || formUrl is null)
        {
            throw new InecobankApiException(TransportErrorCode, "register.do returned no orderId/formUrl.");
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

        using var doc = await PostAsync(InecobankEndpoints.GetOrderStatusExtended, fields, ct);
        var root = doc.RootElement;
        return new InecobankOrderStatus(
            ErrorCode: ReadErrorCode(doc),
            OrderStatus: GetLenientInt(root, "orderStatus"),
            ErrorMessage: ReadErrorMessage(doc),
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

        using var doc = await PostAsync(InecobankEndpoints.Refund, fields, ct);
        ThrowOnError(doc, InecobankEndpoints.Refund);
        _logger.LogInformation(
            "Inecobank refund accepted for gateway order {GatewayOrderId}, amount {AmountMinor}",
            gatewayOrderId, amountMinor);
    }

    /// <summary>Removes characters the gateway forbids and truncates to <see cref="MaxDescriptionLength"/> chars.</summary>
    /// <param name="description">The raw description, possibly null.</param>
    /// <returns>The sanitized description, or null.</returns>
    internal static string? SanitizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var cleaned = string.Concat(description.Where(c => !ForbiddenDescriptionChars.Contains(c)));
        return cleaned.Length <= MaxDescriptionLength ? cleaned : cleaned[..MaxDescriptionLength];
    }

    /// <summary>Posts form-urlencoded fields to a gateway endpoint and parses the JSON response.</summary>
    /// <param name="endpoint">The gateway endpoint name (e.g. "register.do").</param>
    /// <param name="fields">The form fields to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed JSON response document. Caller owns disposal.</returns>
    /// <exception cref="InecobankApiException">
    /// Thrown with <see cref="TransportErrorCode"/> when the request fails outright, the
    /// gateway answers with a non-2xx status, or the response body is not valid JSON — wrapping
    /// the original <see cref="HttpRequestException"/> or <see cref="JsonException"/> as
    /// <see cref="Exception.InnerException"/> so callers only ever need to catch this one type.
    /// </exception>
    private async Task<JsonDocument> PostAsync(
        string endpoint, Dictionary<string, string> fields, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/payment/rest/{endpoint}";
        using var content = new FormUrlEncodedContent(fields);

        HttpResponseMessage? response = null;
        try
        {
            response = await _http.PostAsync(url, content, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            // Deliberately no EnsureSuccessStatusCode(). This gateway reports ordinary business
            // outcomes with a 500 and a JSON body that names the problem -- asking it for an
            // order id it has never seen answers `500 {"message":"Wrong order id","code":"9500"}`.
            // Treating the status as the verdict threw that body away and turned a perfectly
            // well-understood answer into an opaque transport failure, which is what made the
            // admin panel's connection test report failure for credentials that were correct.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new InecobankApiException(
                    UnauthorizedErrorCode,
                    $"{endpoint}: the gateway rejected the merchant credentials.");
            }

            try
            {
                return JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                // Only now is the status worth reporting: the body explained nothing, so the
                // status code is the only thing left to tell an operator what went wrong.
                throw new InecobankApiException(
                    TransportErrorCode,
                    $"{endpoint}: gateway response was not valid JSON (HTTP {(int)response.StatusCode}).",
                    ex);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InecobankApiException(
                TransportErrorCode, $"{endpoint}: request to the gateway failed.", ex);
        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <summary>
    /// Reads the error code out of a gateway response, whichever of the two names it used.
    /// </summary>
    /// <remarks>
    /// The merchant manual documents <c>errorCode</c>, and that is what a 200 response carries.
    /// The failure bodies observed from the live gateway carry <c>code</c> instead -- e.g.
    /// <c>{"message":"Wrong order id","code":"9500"}</c>. Reading only the documented name left
    /// every such response looking like a success with code 0.
    /// </remarks>
    /// <param name="doc">The parsed gateway response.</param>
    /// <returns>The error code, or 0 when neither field is present.</returns>
    private static int ReadErrorCode(JsonDocument doc)
        => GetLenientInt(doc.RootElement, "errorCode")
           ?? GetLenientInt(doc.RootElement, "code")
           ?? 0;

    /// <summary>
    /// Reads the human-readable error text, whichever of the two names the gateway used.
    /// </summary>
    /// <param name="doc">The parsed gateway response.</param>
    /// <returns>The message, or <see langword="null"/> when neither field is present.</returns>
    private static string? ReadErrorMessage(JsonDocument doc)
        => GetString(doc, "errorMessage") ?? GetString(doc, "message");

    /// <summary>Throws <see cref="InecobankApiException"/> when the response's errorCode is non-zero.</summary>
    /// <param name="doc">The parsed gateway response.</param>
    /// <param name="endpoint">The endpoint name, included in the exception message for context.</param>
    /// <exception cref="InecobankApiException">Thrown when the response's errorCode is non-zero.</exception>
    private static void ThrowOnError(JsonDocument doc, string endpoint)
    {
        var code = ReadErrorCode(doc);
        if (code != 0)
        {
            var message = ReadErrorMessage(doc) ?? $"Gateway error {code}";
            throw new InecobankApiException(code, $"{endpoint}: {message}");
        }
    }

    /// <summary>Reads a string property from the response root, or null when absent or not a string.</summary>
    /// <param name="doc">The parsed gateway response.</param>
    /// <param name="property">The property name to read.</param>
    /// <returns>The property's string value, or null when absent or of a different JSON kind.</returns>
    private static string? GetString(JsonDocument doc, string property) =>
        doc.RootElement.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    /// <summary>
    /// Reads an integer property that the gateway inconsistently returns as either a JSON
    /// number or a numeric string.
    /// </summary>
    /// <param name="root">The JSON element to read the property from.</param>
    /// <param name="property">The property name to read.</param>
    /// <returns>The parsed integer, or null when absent or not parseable as an integer.</returns>
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
