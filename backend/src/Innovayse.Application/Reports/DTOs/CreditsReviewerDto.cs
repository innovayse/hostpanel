namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Credits Reviewer report.</summary>
public record CreditsReviewerRowDto(
    int Id,
    int ClientId,
    string ClientName,
    string Date,
    string Description,
    decimal Amount,
    string? AdminUser);

/// <summary>Full Credits Reviewer report result.</summary>
public record CreditsReviewerDto(
    int TotalCount,
    decimal TotalAmount,
    IReadOnlyList<CreditsReviewerRowDto> Rows);
