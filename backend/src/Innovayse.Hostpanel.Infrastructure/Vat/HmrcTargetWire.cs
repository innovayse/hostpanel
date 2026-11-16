namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>The business a VAT number belongs to, as the UK tax authority returns it.</summary>
/// <param name="Name">The registered name.</param>
/// <param name="Address">The registered address.</param>
internal sealed record HmrcTargetWire(string? Name, HmrcAddressWire? Address);
