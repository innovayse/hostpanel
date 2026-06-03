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

    /// <summary>Returns monthly transaction aggregates for the given year.</summary>
    Task<IReadOnlyList<MonthlyTransactionDto>> GetMonthlyTransactionsAsync(int year, CancellationToken ct);

    /// <summary>Returns the top clients ranked by paid-invoice income.</summary>
    Task<IReadOnlyList<TopClientDto>> GetTopClientsAsync(int take, CancellationToken ct);

    /// <summary>Returns income aggregated by product (paid invoice line items).</summary>
    Task<IReadOnlyList<IncomeByProductDto>> GetIncomeByProductAsync(CancellationToken ct);

    /// <summary>Returns client counts and revenue grouped by country.</summary>
    Task<IReadOnlyList<ClientsByCountryDto>> GetClientsByCountryAsync(CancellationToken ct);

    /// <summary>Returns client counts grouped by city and country.</summary>
    Task<IReadOnlyList<ClientsByCityDto>> GetClientsByCityAsync(CancellationToken ct);

    /// <summary>Returns a filtered, paginated list of invoices for reporting.</summary>
    Task<InvoiceReportResultDto> GetInvoicesReportAsync(
        string? status, DateOnly? createdFrom, DateOnly? createdTo,
        DateOnly? dueFrom, DateOnly? dueTo, DateOnly? paidFrom, DateOnly? paidTo,
        int page, int pageSize, CancellationToken ct);

    /// <summary>Returns a filtered, paginated list of transactions for reporting.</summary>
    Task<TransactionReportResultDto> GetTransactionsReportAsync(
        DateOnly? dateFrom, DateOnly? dateTo, string? paymentMethod,
        int page, int pageSize, CancellationToken ct);

    /// <summary>Returns top clients ranked by transaction income.</summary>
    Task<IReadOnlyList<TopClientByIncomeDto>> GetTopClientsByIncomeAsync(int take, CancellationToken ct);

    /// <summary>Returns a client account statement for the given date range.</summary>
    Task<ClientStatementDto> GetClientStatementAsync(int clientId, DateOnly? from, DateOnly? to, CancellationToken ct);
}
