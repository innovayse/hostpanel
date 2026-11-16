namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The income of one calendar day in one currency, still in that currency.</summary>
/// <param name="Day">The calendar day in the host time zone.</param>
/// <param name="Currency">ISO 4217 code of the invoice currency the amount is in.</param>
/// <param name="Amount">Payments less refunds taken that day.</param>
public sealed record IncomeDay(DateOnly Day, string Currency, decimal Amount);
