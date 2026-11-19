namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>One server of the Server Revenue Forecasts report; every amount is in the base currency of <see cref="ServerRevenueForecastsReportDto"/>.</summary>
/// <param name="ServerId">The server identifier.</param>
/// <param name="ServerName">The server's display name.</param>
/// <param name="Hostname">The server's hostname.</param>
/// <param name="ActiveServices">How many active services are provisioned on the server.</param>
/// <param name="MonthlyRevenue">What those services renew for, spread over the months of their cycles.</param>
/// <param name="AnnualRevenue">Twelve months of <paramref name="MonthlyRevenue"/>.</param>
public sealed record ServerRevenueForecastRowDto(
    int ServerId,
    string ServerName,
    string Hostname,
    int ActiveServices,
    decimal MonthlyRevenue,
    decimal AnnualRevenue);
