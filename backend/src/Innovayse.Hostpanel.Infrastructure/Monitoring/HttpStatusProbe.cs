using System.Diagnostics;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Common;

namespace Innovayse.Hostpanel.Infrastructure.Monitoring;

/// <summary>
/// Checks a status page component by requesting the address an operator entered. Registered as a typed
/// <see cref="HttpClient"/> by <c>InfrastructureDependencyInjection</c> behind the outbound address guard, which refuses
/// loopback, link-local and metadata addresses (and private ranges unless the deployment allows them), and with no
/// redirect followed, so the request never leaves the address that was entered.
/// </summary>
public sealed class HttpStatusProbe : IStatusProbe
{
    /// <summary>The typed client the request is made with.</summary>
    private readonly HttpClient _http;

    /// <summary>Initializes the probe.</summary>
    /// <param name="http">The typed client the request is made with.</param>
    public HttpStatusProbe(HttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc/>
    public async Task<StatusProbeResult> ProbeAsync(string url, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var timer = Stopwatch.StartNew();
        try
        {
            // Only the headers are read: the status is the answer, and a large page is never downloaded.
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            // A redirect is not followed, so a 3xx still shows the address answers; 4xx and 5xx do not.
            return new StatusProbeResult((int)response.StatusCode < 400, ElapsedMs(timer));
        }
        catch (Exception ex) when ((ex is HttpRequestException or TimeoutException or OperationCanceledException) && !ct.IsCancellationRequested)
        {
            // A refused address, an unreachable host and a timeout are what a check is there to find, not faults of the job.
            // A cancellation of the job's own token is not caught: that is the host stopping.
            return new StatusProbeResult(false, ElapsedMs(timer));
        }
    }

    /// <summary>Reads the milliseconds a timer has run, clamped to what the record stores.</summary>
    /// <param name="timer">The running timer.</param>
    /// <returns>The elapsed whole milliseconds.</returns>
    private static int ElapsedMs(Stopwatch timer)
    {
        return (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue);
    }
}
