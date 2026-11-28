namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>How amounts in one currency are written on an invoice PDF.</summary>
/// <remarks>
/// The currency's own prefix, suffix and decimal places, from its <c>Currencies</c> row. Writing an
/// amount with them is the renderer's work; this only carries what it needs.
/// </remarks>
/// <param name="Prefix">Text before the amount, e.g. <c>$</c>; may be empty.</param>
/// <param name="Suffix">Text after the amount, e.g. <c> ֏</c>; may be empty.</param>
/// <param name="Decimals">Decimal places shown.</param>
/// <param name="Code">ISO 4217 code of the currency, printed beside the amounts the document stresses.</param>
public sealed record InvoicePdfMoneyFormat(string Prefix, string Suffix, int Decimals, string Code);
