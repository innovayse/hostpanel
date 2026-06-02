namespace Innovayse.Application.Reports.Interfaces;

using Innovayse.Application.Reports.DTOs;

/// <summary>Aggregated data access for reporting queries.</summary>
public interface IReportRepository
{
    /// <summary>Returns daily performance metrics for the given date range.</summary>
    Task<IReadOnlyList<DailyPerformanceDto>> GetDailyPerformanceAsync(DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Returns monthly income totals for the given year.</summary>
    Task<IReadOnlyList<AnnualIncomeDto>> GetAnnualIncomeAsync(int year, CancellationToken ct);

    /// <summary>Returns unpaid invoices with aging buckets.</summary>
    Task<IReadOnlyList<AgingInvoiceDto>> GetAgingInvoicesAsync(CancellationToken ct);

    /// <summary>Returns monthly new customer and order metrics.</summary>
    Task<IReadOnlyList<NewCustomerDto>> GetNewCustomersAsync(int year, CancellationToken ct);
}
