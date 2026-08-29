namespace Innovayse.Application.Reports.Common;

/// <summary>One row in the Credits Reviewer report.</summary>
public record CreditsReviewerRowDto(
    int Id,
    int ClientId,
    string ClientName,
    string Date,
    string Description,
    decimal Amount,
    string? AdminUser);
