using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// VAT number checks for the public demo: no register is ever asked. A number ending in <c>0</c> is not registered, every
/// other one is, so both verdicts can be shown, and the registered name and address are fixed.
/// </summary>
public sealed class DemoVatNumberChecker : IVatNumberChecker
{
    /// <summary>The name a demo business is registered under.</summary>
    private const string DemoName = "Demo Company Ltd";

    /// <summary>The address a demo business is registered at.</summary>
    private const string DemoAddress = "1 Demo Street, Demo City";

    /// <inheritdoc/>
    public Task<VatNumberCheck> CheckAsync(VatNumberLookup lookup, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var answer = lookup.Number.EndsWith('0')
            ? new VatNumberCheck(VatCheckOutcome.Invalid)
            : new VatNumberCheck(VatCheckOutcome.Valid, DemoName, DemoAddress);

        return Task.FromResult(answer);
    }
}
