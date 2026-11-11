using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Reads the live nameservers and TLS certificate of a domain over the network.</summary>
/// <remarks>
/// The domain is text a client typed, so an implementation must refuse to touch anything that is
/// not a public host: IP literals, single-label names and names that resolve to loopback, private,
/// link-local or metadata addresses. A refusal is the empty answer, indistinguishable from a domain
/// that has nothing to read, so the probe cannot be used to map the server's own network.
/// </remarks>
public interface IHostingProbe
{
    /// <summary>Reads the nameservers and the certificate of the domain.</summary>
    /// <param name="input">The domain to probe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What could be read; <see cref="HostingInfoDto.Empty"/> when the domain was refused or nothing answered.</returns>
    public Task<HostingInfoDto> ProbeAsync(HostingProbeInput input, CancellationToken ct);
}
