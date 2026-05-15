namespace Innovayse.Application.Admin.DTOs;

/// <summary>
/// A single data point in the client growth report, representing new clients on a given day.
/// </summary>
/// <param name="Date">The calendar date for this data point.</param>
/// <param name="NewClients">Number of new client accounts registered on this date.</param>
public record ClientGrowthReportItemDto(DateOnly Date, int NewClients);
