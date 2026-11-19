namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>One promotion code of the Promotions report; the amount is in the base currency of <see cref="PromotionsUsageReportDto"/>.</summary>
/// <param name="Code">The promotion code as recorded on the services.</param>
/// <param name="Services">How many services, in any status, carry the code.</param>
/// <param name="ActiveServices">How many of those services are active.</param>
/// <param name="MonthlyRevenue">What the active services renew for, spread over the months of their cycles.</param>
public sealed record PromotionUsageRowDto(
    string Code,
    int Services,
    int ActiveServices,
    decimal MonthlyRevenue);
