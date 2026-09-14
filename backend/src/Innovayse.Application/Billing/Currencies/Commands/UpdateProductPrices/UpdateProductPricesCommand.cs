namespace Innovayse.Application.Billing.Currencies.Commands.UpdateProductPrices;

/// <summary>Rewrites every product's prices in the named currencies from its base-currency prices.</summary>
/// <remarks>
/// A bulk shortcut for the operator, not a rule of the system: a product's price in a currency is
/// whatever is stored, and this writes stored figures — converted at the current rate and rounded
/// to the currency's decimals — that the operator may then edit by hand like any other.
/// </remarks>
/// <param name="Currencies">ISO 4217 alpha codes to recompute, any case; the base may not be among them.</param>
public record UpdateProductPricesCommand(IReadOnlyList<string> Currencies);
