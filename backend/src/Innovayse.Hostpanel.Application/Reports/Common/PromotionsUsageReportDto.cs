namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>The Promotions report: one row per promotion code, every amount in one currency.</summary>
/// <param name="Currency">ISO 4217 code of the base currency every amount of <paramref name="Rows"/> is converted to.</param>
/// <param name="Rows">One row per code, the most used first.</param>
public sealed record PromotionsUsageReportDto(string Currency, IReadOnlyList<PromotionUsageRowDto> Rows);
