using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IHostingProbe"/> standing in for the network. It answers a fixed reading and records
/// every domain it was asked about, so a test can prove which domain the use case probed.
/// </summary>
public sealed class FakeHostingProbe : IHostingProbe
{
    /// <summary>The nameserver every probe reports.</summary>
    public const string Nameserver = "ns1.probe.test";

    /// <summary>The issuer every probe reports.</summary>
    public const string Issuer = "Probe Authority";

    /// <summary>The domains this fake was asked to probe, in call order.</summary>
    public List<string> ProbedDomains { get; } = [];

    /// <inheritdoc/>
    public Task<HostingInfoDto> ProbeAsync(HostingProbeInput input, CancellationToken ct)
    {
        ProbedDomains.Add(input.Domain);

        var now = TestMoments.Seeded;
        return Task.FromResult(new HostingInfoDto([Nameserver], new HostingSslDto(true, Issuer, now.AddDays(-30), now.AddDays(60))));
    }
}
