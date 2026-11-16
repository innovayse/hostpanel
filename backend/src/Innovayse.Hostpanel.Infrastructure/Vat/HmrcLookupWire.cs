namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>The success answer of the UK tax authority's VAT number lookup.</summary>
/// <param name="Target">The registered business the number belongs to.</param>
internal sealed record HmrcLookupWire(HmrcTargetWire? Target);
