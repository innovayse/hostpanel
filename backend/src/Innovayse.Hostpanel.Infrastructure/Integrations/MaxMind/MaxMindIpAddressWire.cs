namespace Innovayse.Hostpanel.Infrastructure.Integrations.MaxMind;

/// <summary>The <c>ip_address</c> member of a minFraud Insights answer.</summary>
/// <param name="Country">The country the address is registered in.</param>
/// <param name="Traits">What kind of address it is.</param>
internal sealed record MaxMindIpAddressWire(MaxMindCountryWire? Country, MaxMindIpTraitsWire? Traits);
