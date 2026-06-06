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

    /// <summary>Returns aging invoices summary grouped by period and currency.</summary>
    Task<AgingInvoiceSummaryDto> GetAgingInvoicesSummaryAsync(CancellationToken ct);

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

    /// <summary>Returns a lightweight list of all clients (id + name) for dropdowns.</summary>
    Task<IReadOnlyList<ClientPickerDto>> GetClientPickerListAsync(CancellationToken ct);

    /// <summary>Returns a client account statement for the given date range.</summary>
    Task<ClientStatementDto> GetClientStatementAsync(int clientId, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Returns monthly orders grouped by product group.</summary>
    Task<MonthlyOrdersDto> GetMonthlyOrdersAsync(int year, int month, CancellationToken ct);

    /// <summary>Returns income by product grouped by product group for a given month.</summary>
    Task<IncomeByProductGroupedDto> GetIncomeByProductGroupedAsync(int year, int month, CancellationToken ct);

    /// <summary>Returns a filtered, paginated list of services for reporting.</summary>
    Task<ServiceReportResultDto> GetServicesReportAsync(
        string? status, string? billingCycle,
        DateOnly? createdFrom, DateOnly? createdTo,
        DateOnly? nextDueFrom, DateOnly? nextDueTo,
        DateOnly? terminatedFrom, DateOnly? terminatedTo,
        int page, int pageSize, CancellationToken ct);

    /// <summary>Returns a filtered, paginated list of domains for reporting.</summary>
    Task<DomainReportResultDto> GetDomainsReportAsync(
        string? status, string? registrar,
        DateOnly? registeredFrom, DateOnly? registeredTo,
        DateOnly? expiresFrom, DateOnly? expiresTo,
        DateOnly? nextDueFrom, DateOnly? nextDueTo,
        int page, int pageSize, CancellationToken ct);

    /// <summary>Returns a filtered, paginated list of clients for reporting.</summary>
    Task<ClientReportResultDto> GetClientsReportAsync(
        string? status, string? country, DateOnly? createdFrom, DateOnly? createdTo,
        int page, int pageSize, CancellationToken ct);

    /// <summary>Returns daily transaction aggregates for a given month.</summary>
    Task<MonthlyTransactionsReportDto> GetDailyTransactionsAsync(int year, int month, CancellationToken ct);

    /// <summary>Returns sales tax liability for the given date range.</summary>
    Task<SalesTaxReportDto> GetSalesTaxReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Returns support ticket replies per admin per day for a given month.</summary>
    Task<SupportTicketRepliesDto> GetSupportTicketRepliesAsync(int year, int month, CancellationToken ct);

    /// <summary>Returns all suspended services with suspension details.</summary>
    Task<IReadOnlyList<ProductSuspensionRowDto>> GetProductSuspensionsAsync(CancellationToken ct);

    /// <summary>Returns credits issued to clients for the given filters.</summary>
    Task<CreditsReviewerDto> GetCreditsReviewerAsync(
        int? clientId, DateOnly? from, DateOnly? to,
        decimal? minAmount, decimal? maxAmount,
        CancellationToken ct);

    /// <summary>Returns average customer retention time grouped by product group.</summary>
    Task<CustomerRetentionDto> GetCustomerRetentionAsync(bool includeActive, CancellationToken ct);

    /// <summary>Returns unpaid invoices assigned to the Direct Debit payment method.</summary>
    Task<DirectDebitDto> GetDirectDebitAsync(CancellationToken ct);

    /// <summary>Returns domain renewal reminder emails with optional filters.</summary>
    Task<DomainRenewalEmailsDto> GetDomainRenewalEmailsAsync(
        int? clientId, string? registrar, string? domain,
        DateOnly? from, DateOnly? to,
        CancellationToken ct);

    /// <summary>Returns ticket feedback comments for a given date range and optional staff filter.</summary>
    Task<TicketFeedbackCommentsDto> GetTicketFeedbackCommentsAsync(
        string? staffName, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Returns per-staff average feedback scores for a given date range.</summary>
    Task<TicketFeedbackScoresDto> GetTicketFeedbackScoresAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Returns rated tickets for review, filtered by min rating and date range.</summary>
    Task<TicketRatingsReviewerDto> GetTicketRatingsReviewerAsync(
        int? minRating, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Returns all ticket tags with usage counts for a given date range.</summary>
    Task<TicketTagsDto> GetTicketTagsAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Returns VAT MOSS settlement data for the given quarter.</summary>
    Task<VatMossDto> GetVatMossAsync(int year, int quarter, CancellationToken ct);
}
