namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>The Server Revenue Forecasts report: one row per server, every amount in one currency.</summary>
/// <param name="Currency">ISO 4217 code of the base currency every amount of <paramref name="Rows"/> is converted to.</param>
/// <param name="Rows">One row per server, the best earning first.</param>
public sealed record ServerRevenueForecastsReportDto(string Currency, IReadOnlyList<ServerRevenueForecastRowDto> Rows);
