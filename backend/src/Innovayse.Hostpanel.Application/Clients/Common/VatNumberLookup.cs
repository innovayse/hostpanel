namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>What <c>IVatNumberChecker</c> is asked to check.</summary>
/// <param name="Country">The ISO 3166-1 alpha-2 code of the country the number is registered in, upper case.</param>
/// <param name="Number">The number without its country prefix, spaces or punctuation, upper case.</param>
public sealed record VatNumberLookup(string Country, string Number);
