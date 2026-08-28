namespace Innovayse.Application.Reports.Queries.GetIncomeForecast;

/// <summary>One month of the Income Forecast report.</summary>
/// <param name="Month">Month label, as the annual income report names it.</param>
/// <param name="Monthly">Income booked in this month alone.</param>
/// <param name="Quarterly">Income over this month and the two before it.</param>
/// <param name="SemiAnnual">Income over this month and the five before it.</param>
/// <param name="Annual">Income over the whole year, repeated on every row for comparison.</param>
/// <param name="Total">Income from January up to and including this month.</param>
public record IncomeForecastRowDto(
    string Month,
    decimal Monthly,
    decimal Quarterly,
    decimal SemiAnnual,
    decimal Annual,
    decimal Total);
