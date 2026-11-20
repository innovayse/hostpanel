namespace Innovayse.Hostpanel.Application.Billing.Currencies.Common;

/// <summary>One amount after conversion, with the number of decimals it is meant to be shown to.</summary>
/// <param name="Amount">The converted amount, or the original one when it could not be converted.</param>
/// <param name="Decimals">Decimals of the target currency, or two when the amount was left unconverted.</param>
/// <param name="Converted">Whether the amount was converted through a stored rate; false when its own currency has no stored row.</param>
public sealed record ConvertedAmountDto(decimal Amount, int Decimals, bool Converted);
