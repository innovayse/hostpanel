namespace Innovayse.Application.Reports.Common;

/// <summary>Full Credits Reviewer report result.</summary>
public record CreditsReviewerDto(
    int TotalCount,
    decimal TotalAmount,
    IReadOnlyList<CreditsReviewerRowDto> Rows);
