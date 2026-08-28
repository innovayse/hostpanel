namespace Innovayse.Application.Reports.Queries.GetIncomeForecast;

/// <summary>Query for the Income Forecast report.</summary>
/// <param name="Year">Calendar year to forecast, or null for the current UTC year.</param>
public record GetIncomeForecastQuery(int? Year = null);
