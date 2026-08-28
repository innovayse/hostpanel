namespace Innovayse.Application.Reports.DTOs;

/// <summary>Per-staff feedback score summary with individual rating counts.</summary>
/// <param name="StaffName">Admin the scores belong to.</param>
/// <param name="R1">Number of ratings of 1.</param>
/// <param name="R2">Number of ratings of 2.</param>
/// <param name="R3">Number of ratings of 3.</param>
/// <param name="R4">Number of ratings of 4.</param>
/// <param name="R5">Number of ratings of 5.</param>
/// <param name="R6">Number of ratings of 6.</param>
/// <param name="R7">Number of ratings of 7.</param>
/// <param name="R8">Number of ratings of 8.</param>
/// <param name="R9">Number of ratings of 9.</param>
/// <param name="R10">Number of ratings of 10.</param>
/// <param name="TotalRatings">Total number of ratings received.</param>
/// <param name="AverageRating">Mean of every rating received.</param>
public record TicketFeedbackScoreRowDto(
    string StaffName,
    int R1, int R2, int R3, int R4, int R5,
    int R6, int R7, int R8, int R9, int R10,
    int TotalRatings,
    double AverageRating);
