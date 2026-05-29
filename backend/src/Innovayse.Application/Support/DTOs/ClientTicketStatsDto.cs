namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO containing ticket statistics for a client, broken down by time period.</summary>
/// <param name="OpenedThisMonth">Number of tickets opened in the current calendar month.</param>
/// <param name="OpenedLastMonth">Number of tickets opened in the previous calendar month.</param>
/// <param name="OpenedThisYear">Number of tickets opened in the current calendar year.</param>
/// <param name="OpenedLastYear">Number of tickets opened in the previous calendar year.</param>
public record ClientTicketStatsDto(
    int OpenedThisMonth,
    int OpenedLastMonth,
    int OpenedThisYear,
    int OpenedLastYear);
