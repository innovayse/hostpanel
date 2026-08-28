namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Customer Retention report.</summary>
/// <param name="ProductName">Product the retention figures were measured for.</param>
/// <param name="BillingCycle">Billing cycle the figures were measured for.</param>
/// <param name="ProductCount">Number of services the average was taken over.</param>
/// <param name="AvgDaysActive">Mean number of days a service stayed active.</param>
/// <param name="AvgYearsMonthsActive">The same average rendered as years and months for display.</param>
public record CustomerRetentionRowDto(
    string ProductName,
    string BillingCycle,
    int ProductCount,
    int AvgDaysActive,
    string AvgYearsMonthsActive);
