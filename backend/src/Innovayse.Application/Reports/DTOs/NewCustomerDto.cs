namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the New Customers report.</summary>
public record NewCustomerDto(
    string Month,
    int NewSignups,
    int OrdersPlaced,
    int OrdersCompleted,
    double ConversionRate);
