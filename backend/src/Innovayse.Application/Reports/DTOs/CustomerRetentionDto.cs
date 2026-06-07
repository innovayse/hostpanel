namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Customer Retention report.</summary>
public record CustomerRetentionRowDto(
    string ProductName,
    string BillingCycle,
    int ProductCount,
    int AvgDaysActive,
    string AvgYearsMonthsActive);

/// <summary>One group (product group) in the Customer Retention report.</summary>
public record CustomerRetentionGroupDto(
    string GroupName,
    IReadOnlyList<CustomerRetentionRowDto> Rows);

/// <summary>Full Customer Retention report result.</summary>
public record CustomerRetentionDto(
    IReadOnlyList<CustomerRetentionGroupDto> Groups);
