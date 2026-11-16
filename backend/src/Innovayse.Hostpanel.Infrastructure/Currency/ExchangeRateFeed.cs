using Innovayse.Hostpanel.Infrastructure.Resilience;
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
    /// <summary>
    /// The most bytes of a document the feed reads. A published set is a few kilobytes; a body past this is not
    /// one, and reading it whole would let a hostile or broken answer fill the process memory.
    /// </summary>
    public const long MaxBodyBytes = 1024 * 1024;

    /// <summary>
    /// Aims a typed client at a bank, caps what it buffers of an answer at <see cref="MaxBodyBytes"/> and bounds the
    /// whole call, body included, by <see cref="HttpBackstopTimeouts.ExchangeRate"/>.
    /// </summary>
    /// <remarks>
    /// The client buffers the body itself, after the resilience pipeline has handed back the headers, so the pipeline's
    /// timeouts do not cover it. The cap (<see cref="HttpClient.MaxResponseContentBufferSize"/>) fails a body past it as an
    /// <see cref="HttpRequestException"/> instead of reading it whole; the client's own timeout cancels a body that
    /// stalls, which <see cref="TryReadAsync"/> reports like a bank that did not answer in time.
    /// </remarks>
    /// <param name="client">The typed client being registered.</param>
    /// <param name="baseAddress">The bank's origin.</param>
    public static void Configure(HttpClient client, Uri baseAddress)
    {
        client.BaseAddress = baseAddress;
        client.MaxResponseContentBufferSize = MaxBodyBytes;
        client.Timeout = HttpBackstopTimeouts.ExchangeRate;
    }

    /// <summary>Downloads the document at <paramref name="path"/> and returns its text.</summary>
    /// <param name="http">The typed client, with the bank's origin as its base address.</param>
    /// <param name="path">The document, relative to the client's base address.</param>
    /// <param name="logger">Records why the download failed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The body, or <see langword="null"/> when the bank did not answer, answered with an error status,
    /// answered with more than <see cref="MaxBodyBytes"/> or the resilience pipeline gave up; the cause is logged.
    /// </returns>
    public static async Task<string?> TryReadAsync(HttpClient http, string path, ILogger logger, CancellationToken ct)
    {
        try
        {
            // The client buffers the body under MaxResponseContentBufferSize (see Configure), so an oversized
            // body surfaces here as an HttpRequestException.
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
