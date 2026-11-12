using Innovayse.Hostpanel.Application.StatusPage.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Requests the address a status page component is monitored by and reports whether it answered as expected.</summary>
public interface IStatusProbe
{
    /// <summary>
    /// Requests <paramref name="url"/> once. A refused address, an unreachable host, a timeout and an error status are
    /// all a failed check, not an exception: the caller counts failures.
    /// </summary>
    /// <param name="url">The address to request; an absolute http or https address an operator entered.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the request succeeded and how long it took.</returns>
    public Task<StatusProbeResult> ProbeAsync(string url, CancellationToken ct);
}
