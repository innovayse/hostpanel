using Microsoft.Extensions.Logging;
using Polly;

namespace Innovayse.Hostpanel.Infrastructure.Currency;

/// <summary>
/// The one download every rate source makes: a GET of the bank's published document. Whatever goes
/// wrong on the way is logged with its cause and reported as "no body", so each adapter answers
/// <c>EXCHANGE_RATE_SOURCE_UNAVAILABLE</c> the same way and the bank's own text never reaches a response.
/// </summary>
public static class ExchangeRateFeed
{
    /// <summary>Downloads the document at <paramref name="path"/> and returns its text.</summary>
    /// <param name="http">The typed client, with the bank's origin as its base address.</param>
    /// <param name="path">The document, relative to the client's base address.</param>
    /// <param name="logger">Records why the download failed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The body, or <see langword="null"/> when the bank did not answer, answered with an error status
    /// or the resilience pipeline gave up; the cause is logged.
    /// </returns>
    public static async Task<string?> TryReadAsync(HttpClient http, string path, ILogger logger, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "The exchange rate document {Path} could not be fetched.", path);
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation the caller never asked for.
            logger.LogWarning(ex, "The exchange rate document {Path} did not answer in time.", path);
            return null;
        }
        catch (ExecutionRejectedException ex)
        {
            // The resilience pipeline gave up: its total timeout elapsed or its breaker is open.
            logger.LogWarning(ex, "The exchange rate document {Path} was refused by the resilience pipeline.", path);
            return null;
        }
    }
}
