using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.Extensions.Logging;
using Polly;

namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>Checks a VAT number of an EU member state against the European Commission's VIES service.</summary>
/// <remarks>
/// <para>
/// <b>Endpoint and shape.</b> <c>POST {BaseAddress}check-vat-number</c> with <c>{ "countryCode": "DE", "vatNumber": "123456789" }</c>
/// answers <c>{ "valid": true, "name": "...", "address": "...", "userError": "VALID", ... }</c>. A member state that does not
/// disclose the trader sends dashes (<c>---</c>) for the name and address, which are read as "none".
/// </para>
/// <para>
/// <b>Verdicts.</b> <c>valid: true</c> is valid. <c>userError</c> <c>INVALID</c> or <c>INVALID_INPUT</c> with <c>valid: false</c>
/// is invalid. Everything else is unavailable: the member state's own service is down or busy (<c>MS_UNAVAILABLE</c>,
/// <c>MS_MAX_CONCURRENT_REQ</c>, <c>TIMEOUT</c>, <c>SERVICE_UNAVAILABLE</c> and the like), an error status, a body that is
/// not the published object, no answer in time. Those are never reported as invalid, because a number must not lose its
/// reverse charge because a register was busy.
/// </para>
/// </remarks>
public sealed class ViesVatClient
{
    /// <summary>The service's origin and API root; the typed client registration sets it as the base address.</summary>
    public const string BaseAddress = "https://ec.europa.eu/taxation_customs/vies/rest-api/";

    /// <summary>The check route, relative to <see cref="BaseAddress"/>.</summary>
    public const string CheckPath = "check-vat-number";

    /// <summary>What the service sends for a name or address the member state does not disclose.</summary>
    private const string Undisclosed = "---";

    /// <summary>The service's verdict for a number that is not registered.</summary>
    private const string InvalidVerdict = "INVALID";

    /// <summary>The service's verdict for a number whose shape no member state accepts.</summary>
    private const string InvalidInputVerdict = "INVALID_INPUT";

    /// <summary>The typed client, with the service's origin as its base address.</summary>
    private readonly HttpClient _http;

    /// <summary>Records why a check was not completed; the answer carries only the outcome.</summary>
    private readonly ILogger<ViesVatClient> _logger;

    /// <summary>Initializes a new instance of the <see cref="ViesVatClient"/> class.</summary>
    /// <param name="http">The typed client, with the service's origin as its base address.</param>
    /// <param name="logger">Records why a check was not completed.</param>
    public ViesVatClient(HttpClient http, ILogger<ViesVatClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>Checks one number.</summary>
    /// <param name="lookup">The member state and the number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Valid with what the service disclosed, invalid, or unavailable; never throws for a service that did not answer.</returns>
    public async Task<VatNumberCheck> CheckAsync(VatNumberLookup lookup, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        ViesCheckWire? answer;
        try
        {
            using var response = await _http.PostAsJsonAsync(CheckPath, new ViesLookupWire(VatRegisters.PrefixOf(lookup.Country), lookup.Number), ct);
            response.EnsureSuccessStatusCode();
            answer = await response.Content.ReadFromJsonAsync<ViesCheckWire>(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "The VIES service could not be reached for a {Country} number.", lookup.Country);
            return new VatNumberCheck(VatCheckOutcome.Unavailable);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation the caller never asked for.
            _logger.LogWarning(ex, "The VIES service did not answer in time for a {Country} number.", lookup.Country);
            return new VatNumberCheck(VatCheckOutcome.Unavailable);
        }
        catch (ExecutionRejectedException ex)
        {
            // The resilience pipeline gave up: its total timeout elapsed or its breaker is open.
            _logger.LogWarning(ex, "The VIES service was refused by the resilience pipeline for a {Country} number.", lookup.Country);
            return new VatNumberCheck(VatCheckOutcome.Unavailable);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The VIES service did not answer with the published object for a {Country} number.", lookup.Country);
            return new VatNumberCheck(VatCheckOutcome.Unavailable);
        }

        return Interpret(answer, lookup.Country);
    }

    /// <summary>Turns the service's answer into a verdict.</summary>
    /// <param name="answer">The parsed answer, or null for an empty body.</param>
    /// <param name="country">The country checked, for the log.</param>
    /// <returns>The verdict.</returns>
    private VatNumberCheck Interpret(ViesCheckWire? answer, string country)
    {
        if (answer is { Valid: true })
        {
            return new VatNumberCheck(VatCheckOutcome.Valid, Disclosed(answer.Name), Disclosed(answer.Address));
        }

        if (answer is { Valid: false, UserError: InvalidVerdict or InvalidInputVerdict })
        {
            return new VatNumberCheck(VatCheckOutcome.Invalid);
        }

        _logger.LogWarning("The VIES service gave no verdict for a {Country} number ({Verdict}).", country, answer?.UserError);
        return new VatNumberCheck(VatCheckOutcome.Unavailable);
    }

    /// <summary>Reads a name or address the service returned, dropping the placeholder it sends when none is disclosed.</summary>
    /// <param name="text">The text as sent.</param>
    /// <returns>The text, or <see langword="null"/> when it is blank or the placeholder.</returns>
    private static string? Disclosed(string? text) =>
        string.IsNullOrWhiteSpace(text) || text.Trim() == Undisclosed ? null : text.Trim();
}
