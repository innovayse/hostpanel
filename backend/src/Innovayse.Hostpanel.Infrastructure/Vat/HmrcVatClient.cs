using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Vat.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>Checks a United Kingdom VAT number against the UK tax authority's VAT number lookup.</summary>
/// <remarks>
/// <para>
/// <b>Endpoint and shape.</b> <c>GET {BaseUrl}organisations/vat/check-vat-number/lookup/{number}</c>, asked for version 2.0 of
/// the API with a bearer token. A registered number answers 200 <c>{ "target": { "name": "...", "address": { "line1": "...", ... } } }</c>.
/// </para>
/// <para>
/// <b>Token.</b> The lookup is application restricted, so a token is requested first with the credentials of
/// <see cref="HmrcVatOptions"/> (<c>POST oauth/token</c>, client credentials). It is requested for every check rather than kept:
/// checks are rare, and a kept token would need a lock and an expiry rule for no gain. Without credentials the answer is unavailable.
/// </para>
/// <para>
/// <b>Verdicts.</b> 200 is valid. 404 <c>NOT_FOUND</c> (not registered) and 400 <c>INVALID_REQUEST</c> (not a number) are invalid.
/// Everything else is unavailable: no credentials, a token that was refused, an authority that is down or rate limiting,
/// any other status or code, a body that is not the published object, no answer in time. A refusal that is about the
/// request rather than the number (a wrong header, a rejected token) is therefore never reported as an invalid number.
/// </para>
/// </remarks>
public sealed class HmrcVatClient
{
    /// <summary>The lookup route, relative to the base address; the number is appended.</summary>
    public const string LookupPath = "organisations/vat/check-vat-number/lookup/";

    /// <summary>The token route, relative to the base address.</summary>
    public const string TokenPath = "oauth/token";

    /// <summary>The media type that asks for version 2.0 of the API.</summary>
    private const string AcceptVersion = "application/vnd.hmrc.2.0+json";

    /// <summary>The error code of a number that is not registered.</summary>
    private const string NotFoundCode = "NOT_FOUND";

    /// <summary>The error code of a number that is not shaped like one.</summary>
    private const string InvalidRequestCode = "INVALID_REQUEST";

    /// <summary>The typed client, with the authority's API root as its base address.</summary>
    private readonly HttpClient _http;

    /// <summary>The credentials of the registered application.</summary>
    private readonly IOptions<HmrcVatOptions> _options;

    /// <summary>Records why a check was not completed; the answer carries only the outcome.</summary>
    private readonly ILogger<HmrcVatClient> _logger;

    /// <summary>Initializes a new instance of the <see cref="HmrcVatClient"/> class.</summary>
    /// <param name="http">The typed client, with the authority's API root as its base address.</param>
    /// <param name="options">The credentials of the registered application.</param>
    /// <param name="logger">Records why a check was not completed.</param>
    public HmrcVatClient(HttpClient http, IOptions<HmrcVatOptions> options, ILogger<HmrcVatClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    /// <summary>Checks one number.</summary>
    /// <param name="lookup">The United Kingdom and the number without its prefix.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Valid with what the authority disclosed, invalid, or unavailable; never throws for a service that did not answer.</returns>
    public async Task<VatNumberCheck> CheckAsync(VatNumberLookup lookup, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        if (!_options.Value.IsConfigured)
        {
            _logger.LogInformation("A United Kingdom VAT number was not checked: no credentials are configured for the lookup.");
            return new VatNumberCheck(VatCheckOutcome.Unavailable);
        }

        try
        {
            var token = await RequestTokenAsync(ct);
            if (token is null)
            {
                return new VatNumberCheck(VatCheckOutcome.Unavailable);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, LookupPath + Uri.EscapeDataString(lookup.Number));
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(AcceptVersion));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, ct);
            return await InterpretAsync(response, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "The UK VAT number lookup could not be reached.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation the caller never asked for.
            _logger.LogWarning(ex, "The UK VAT number lookup did not answer in time.");
        }
        catch (ExecutionRejectedException ex)
        {
            // The resilience pipeline gave up: its total timeout elapsed or its breaker is open.
            _logger.LogWarning(ex, "The UK VAT number lookup was refused by the resilience pipeline.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The UK VAT number lookup did not answer with the published object.");
        }

        return new VatNumberCheck(VatCheckOutcome.Unavailable);
    }

    /// <summary>Asks the authority for a token with the application's credentials.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bearer token, or <see langword="null"/> when the authority refused or sent none.</returns>
    private async Task<string?> RequestTokenAsync(CancellationToken ct)
    {
        var options = _options.Value;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["grant_type"] = "client_credentials",
        });

        using var response = await _http.PostAsync(TokenPath, form, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("The UK tax authority refused the token request with {Status}.", (int)response.StatusCode);
            return null;
        }

        var token = await response.Content.ReadFromJsonAsync<HmrcTokenWire>(ct);
        return string.IsNullOrEmpty(token?.AccessToken) ? null : token.AccessToken;
    }

    /// <summary>Turns the lookup's answer into a verdict.</summary>
    /// <param name="response">The lookup's answer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verdict.</returns>
    private async Task<VatNumberCheck> InterpretAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var found = await response.Content.ReadFromJsonAsync<HmrcLookupWire>(ct);
            return found?.Target is { } target
                ? new VatNumberCheck(VatCheckOutcome.Valid, Trimmed(target.Name), target.Address?.ToSingleLine())
                : Unavailable(response);
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadFromJsonAsync<HmrcErrorWire>(ct);
            var refusedTheNumber = (response.StatusCode == HttpStatusCode.NotFound && error?.Code == NotFoundCode)
                || (response.StatusCode == HttpStatusCode.BadRequest && error?.Code == InvalidRequestCode);
            if (refusedTheNumber)
            {
                return new VatNumberCheck(VatCheckOutcome.Invalid);
            }
        }

        return Unavailable(response);
    }

    /// <summary>Logs a lookup that gave no verdict and answers unavailable.</summary>
    /// <param name="response">The lookup's answer.</param>
    /// <returns>An unavailable answer.</returns>
    private VatNumberCheck Unavailable(HttpResponseMessage response)
    {
        _logger.LogWarning("The UK VAT number lookup gave no verdict (status {Status}).", (int)response.StatusCode);
        return new VatNumberCheck(VatCheckOutcome.Unavailable);
    }

    /// <summary>Trims a text the authority returned.</summary>
    /// <param name="text">The text as sent.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is blank.</returns>
    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
