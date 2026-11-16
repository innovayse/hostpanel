using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>
/// The one <see cref="IVatNumberChecker"/>: asks the register that holds the number's country, the UK tax authority's for the
/// United Kingdom and the European Commission's for every EU member state, so no handler learns which service answered.
/// </summary>
public sealed class VatNumberChecker : IVatNumberChecker
{
    /// <summary>The EU register.</summary>
    private readonly ViesVatClient _vies;

    /// <summary>The United Kingdom's register.</summary>
    private readonly HmrcVatClient _hmrc;

    /// <summary>Initializes a new instance of the <see cref="VatNumberChecker"/> class.</summary>
    /// <param name="vies">The EU register.</param>
    /// <param name="hmrc">The United Kingdom's register.</param>
    public VatNumberChecker(ViesVatClient vies, HmrcVatClient hmrc)
    {
        _vies = vies;
        _hmrc = hmrc;
    }

    /// <inheritdoc/>
    public Task<VatNumberCheck> CheckAsync(VatNumberLookup lookup, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        return lookup.Country == VatRegisters.UnitedKingdom
            ? _hmrc.CheckAsync(lookup, ct)
            : _vies.CheckAsync(lookup, ct);
    }
}
