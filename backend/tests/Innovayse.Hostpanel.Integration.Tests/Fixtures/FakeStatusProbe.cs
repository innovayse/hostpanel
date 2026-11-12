using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A configurable <see cref="IStatusProbe"/> standing in for a real request to a component's address.
/// Every address answers successfully until a test marks it failing with <see cref="Fail"/>.
/// </summary>
public sealed class FakeStatusProbe : IStatusProbe
{
    /// <summary>The addresses that currently fail; the value is unused.</summary>
    private readonly ConcurrentDictionary<string, bool> _failing = new();

    /// <summary>The addresses this fake was asked to request, in call order.</summary>
    private readonly ConcurrentQueue<string> _requested = new();

    /// <summary>Gets the addresses this fake was asked to request, in call order.</summary>
    public IReadOnlyCollection<string> Requested => _requested;

    /// <summary>Makes an address fail from now on.</summary>
    /// <param name="url">The address.</param>
    public void Fail(string url)
    {
        _failing[url] = true;
    }

    /// <summary>Makes an address answer successfully again.</summary>
    /// <param name="url">The address.</param>
    public void Recover(string url)
    {
        _failing.TryRemove(url, out _);
    }

    /// <inheritdoc/>
    public Task<StatusProbeResult> ProbeAsync(string url, CancellationToken ct)
    {
        _requested.Enqueue(url);
        return Task.FromResult(new StatusProbeResult(!_failing.ContainsKey(url), 12));
    }
}
