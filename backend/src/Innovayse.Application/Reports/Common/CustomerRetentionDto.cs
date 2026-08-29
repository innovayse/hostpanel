namespace Innovayse.Application.Reports.Common;

/// <summary>Full Customer Retention report result.</summary>
/// <param name="Groups">Retention figures grouped by product group.</param>
public record CustomerRetentionDto(
    IReadOnlyList<CustomerRetentionGroupDto> Groups);
