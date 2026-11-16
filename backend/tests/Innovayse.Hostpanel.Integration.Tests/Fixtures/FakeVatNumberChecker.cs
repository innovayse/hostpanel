using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IVatNumberChecker"/> that answers what a test told it to and records what it was asked for. A fake port
/// standing in for the VAT number services, which a test must not call.
/// </summary>
/// <param name="answer">Decides the answer for a lookup; the default says every number is registered.</param>
/// <param name="configured">Decides whether the register of a country is set up; the default says every one is.</param>
public sealed class FakeVatNumberChecker(
    Func<VatNumberLookup, VatNumberCheck>? answer = null,
    Func<string, bool>? configured = null) : IVatNumberChecker
{
    /// <summary>The lookups received, in arrival order.</summary>
    private readonly ConcurrentQueue<VatNumberLookup> lookups = new();

    /// <summary>The answer for a lookup.</summary>
    private readonly Func<VatNumberLookup, VatNumberCheck> decide = answer
        ?? (_ => new VatNumberCheck(VatCheckOutcome.Valid, "Registered Name", "Registered Street 1"));

    /// <summary>Whether the register of a country is set up.</summary>
    private readonly Func<string, bool> isConfigured = configured ?? (_ => true);

    /// <summary>Gets the lookups received so far, in arrival order.</summary>
    public IReadOnlyList<VatNumberLookup> Lookups => [.. lookups];

    /// <inheritdoc />
    public bool IsConfiguredFor(string country) => isConfigured(country);

    /// <inheritdoc />
    public Task<VatNumberCheck> CheckAsync(VatNumberLookup lookup, CancellationToken ct)
    {
        lookups.Enqueue(lookup);
        return Task.FromResult(decide(lookup));
    }
}
