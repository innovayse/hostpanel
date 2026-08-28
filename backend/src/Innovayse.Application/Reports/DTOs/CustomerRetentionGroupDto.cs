namespace Innovayse.Application.Reports.DTOs;

/// <summary>One product group in the Customer Retention report.</summary>
/// <param name="GroupName">Name of the product group.</param>
/// <param name="Rows">Retention figures for each product and billing cycle in the group.</param>
public record CustomerRetentionGroupDto(
    string GroupName,
    IReadOnlyList<CustomerRetentionRowDto> Rows);
