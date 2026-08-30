namespace Innovayse.API.Reports;

using Innovayse.API.RateLimiting;
using System.Text;
using Innovayse.Application.Reports.Commands.RevalidateSslMonitoring;
using Innovayse.Application.Reports.Commands.UpdateDiskUsage;
using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Queries.AgingInvoices;
using Innovayse.Application.Reports.Queries.AgingInvoicesSummary;
using Innovayse.Application.Reports.Queries.AnnualIncome;
using Innovayse.Application.Reports.Queries.ClientStatement;
using Innovayse.Application.Reports.Queries.ClientsByCity;
using Innovayse.Application.Reports.Queries.ClientsByCountry;
using Innovayse.Application.Reports.Queries.DailyPerformance;
using Innovayse.Application.Reports.Queries.GetClientPicker;
using Innovayse.Application.Reports.Queries.GetClientsReport;
using Innovayse.Application.Reports.Queries.GetCreditsReviewer;
using Innovayse.Application.Reports.Queries.GetCustomerRetention;
using Innovayse.Application.Reports.Queries.GetDailyTransactions;
using Innovayse.Application.Reports.Queries.GetDirectDebit;
using Innovayse.Application.Reports.Queries.GetDiskUsageReport;
using Innovayse.Application.Reports.Queries.GetDomainRenewalEmails;
using Innovayse.Application.Reports.Queries.GetDomainsReport;
using Innovayse.Application.Reports.Queries.GetIncomeByProductGrouped;
using Innovayse.Application.Reports.Queries.GetIncomeForecast;
using Innovayse.Application.Reports.Queries.GetMonthlyOrders;
using Innovayse.Application.Reports.Queries.GetProductSuspensions;
using Innovayse.Application.Reports.Queries.GetSalesTaxReport;
using Innovayse.Application.Reports.Queries.GetServicesReport;
using Innovayse.Application.Reports.Queries.GetSslMonitoring;
using Innovayse.Application.Reports.Queries.GetSupportTicketReplies;
using Innovayse.Application.Reports.Queries.GetTicketFeedbackComments;
using Innovayse.Application.Reports.Queries.GetTicketFeedbackScores;
using Innovayse.Application.Reports.Queries.GetTicketRatingsReviewer;
using Innovayse.Application.Reports.Queries.GetTicketTags;
using Innovayse.Application.Reports.Queries.GetVatMoss;
using Innovayse.Application.Reports.Queries.InvoicesReport;
using Innovayse.Application.Reports.Queries.NewCustomers;
using Innovayse.Application.Reports.Queries.TopClientsByIncome;
using Innovayse.Application.Reports.Queries.TransactionsReport;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

/// <summary>Admin reporting endpoints. Every action binds, dispatches one message, and returns.</summary>
/// <remarks>
/// <para>
/// <b>Every date filter binds as <see cref="DateOnly"/>, never as a <see langword="string"/> the
/// action parses itself.</b> Forty-eight query parameters on this controller used to arrive as
/// <c>string?</c> and be turned into a date with <c>DateOnly.Parse</c> inside the action. That is
/// two defects in one line. It is logic in an HTTP entry point, which
/// <c>rules/clean-architecture.md</c> forbids; and <c>DateOnly.Parse</c> throws
/// <see cref="FormatException"/>, which <c>ExceptionMiddleware</c> does not classify, so
/// <c>?from=notadate</c> answered <b>500</b> on at least six routes here rather than telling the
/// caller their input was wrong.
/// </para>
/// <para>
/// Binding the parameter as <c>DateOnly?</c> hands the parse to ASP.NET Core model binding, which
/// answers <b>400</b> with the same <c>ProblemDetails</c> body this controller already returns for
/// a malformed <c>int</c> parameter such as <c>?take=abc</c> — so a bad date and a bad number now
/// fail alike. No <c>ValidationMessages</c> entry is involved: this is a binding failure the
/// framework describes, not a domain refusal a person is meant to read.
/// </para>
/// <para>
/// A date parameter added here must therefore be declared <c>DateOnly?</c>. Reintroducing
/// <c>string?</c> plus a parse reintroduces the 500.
/// </para>
/// </remarks>
/// <param name="bus">Wolverine bus every action dispatches through.</param>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ReportsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns daily performance metrics.</summary>
    /// <param name="from">Start date (yyyy-MM-dd). Defaults to 30 days ago.</param>
    /// <param name="to">End date (yyyy-MM-dd). Defaults to today.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("daily-performance")]
    public async Task<ActionResult<IReadOnlyList<DailyPerformanceDto>>> GetDailyPerformanceAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? toDate.AddDays(-29);
        var result = await bus.InvokeAsync<IReadOnlyList<DailyPerformanceDto>>(
            new DailyPerformanceQuery(fromDate, toDate), ct);
        return Ok(result);
    }

    /// <summary>Returns monthly income totals for the given year.</summary>
    /// <param name="year">Year (defaults to current year).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("annual-income")]
    public async Task<ActionResult<IReadOnlyList<AnnualIncomeDto>>> GetAnnualIncomeAsync(
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<AnnualIncomeDto>>(
            new AnnualIncomeQuery(year ?? DateTime.UtcNow.Year), ct);
        return Ok(result);
    }

    /// <summary>Returns unpaid invoices grouped by days outstanding.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("aging-invoices")]
    public async Task<ActionResult<IReadOnlyList<AgingInvoiceDto>>> GetAgingInvoicesAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<AgingInvoiceDto>>(new AgingInvoicesQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns aging invoices summary grouped by period and currency.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("aging-invoices-summary")]
    public async Task<ActionResult<AgingInvoiceSummaryDto>> GetAgingInvoicesSummaryAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<AgingInvoiceSummaryDto>(
            new AgingInvoicesSummaryQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns income forecast based on recurring billing cycles.</summary>
    /// <param name="year">Year (defaults to current year).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("income-forecast")]
    public async Task<ActionResult<IReadOnlyList<IncomeForecastRowDto>>> GetIncomeForecastAsync(
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<IncomeForecastRowDto>>(
            new GetIncomeForecastQuery(year), ct);
        return Ok(result);
    }

    /// <summary>Returns monthly new customer and order metrics.</summary>
    /// <param name="year">Year (defaults to current year).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("new-customers")]
    public async Task<ActionResult<IReadOnlyList<NewCustomerDto>>> GetNewCustomersAsync(
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<NewCustomerDto>>(
            new NewCustomersQuery(year ?? DateTime.UtcNow.Year), ct);
        return Ok(result);
    }

    /// <summary>Returns a filtered, paginated list of invoices.</summary>
    [HttpGet("invoices")]
    public async Task<ActionResult<InvoiceReportResultDto>> GetInvoicesReportAsync(
        [FromQuery] string? status,
        [FromQuery] DateOnly? createdFrom, [FromQuery] DateOnly? createdTo,
        [FromQuery] DateOnly? dueFrom, [FromQuery] DateOnly? dueTo,
        [FromQuery] DateOnly? paidFrom, [FromQuery] DateOnly? paidTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<InvoiceReportResultDto>(new InvoicesReportQuery(
            status,
            createdFrom,
            createdTo,
            dueFrom,
            dueTo,
            paidFrom,
            paidTo,
            page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Exports invoices report as CSV.</summary>
    [HttpGet("invoices/export")]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    public async Task<IActionResult> ExportInvoicesCsvAsync(
        [FromQuery] string? status,
        [FromQuery] DateOnly? createdFrom, [FromQuery] DateOnly? createdTo,
        [FromQuery] DateOnly? dueFrom, [FromQuery] DateOnly? dueTo,
        [FromQuery] DateOnly? paidFrom, [FromQuery] DateOnly? paidTo,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<InvoiceReportResultDto>(new InvoicesReportQuery(
            status,
            createdFrom,
            createdTo,
            dueFrom,
            dueTo,
            paidFrom,
            paidTo,
            1, 10000), ct);

        var sb = new StringBuilder();
        sb.AppendLine("ID,Client ID,Client Name,Status,Created Date,Due Date,Date Paid,SubTotal,Credit,Tax,Tax Rate,Total,Payment Method,Notes");
        foreach (var r in result.Items)
        {
            sb.AppendLine($"{r.Id},{r.ClientId},\"{r.ClientName}\",{r.Status},{r.CreatedDate},{r.DueDate},{r.DatePaid},{r.SubTotal},{r.Credit},{r.Tax},{r.TaxRate},{r.Total},\"{r.PaymentMethod}\",\"{r.Notes}\"");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "invoices-report.csv");
    }

    /// <summary>Returns a filtered, paginated list of transactions.</summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<TransactionReportResultDto>> GetTransactionsReportAsync(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] string? paymentMethod,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TransactionReportResultDto>(new TransactionsReportQuery(
            dateFrom,
            dateTo,
            paymentMethod, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Exports transactions report as CSV.</summary>
    [HttpGet("transactions/export")]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    public async Task<IActionResult> ExportTransactionsCsvAsync(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] string? paymentMethod,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TransactionReportResultDto>(new TransactionsReportQuery(
            dateFrom,
            dateTo,
            paymentMethod, 1, 10000), ct);

        var sb = new StringBuilder();
        sb.AppendLine("ID,Client ID,Client Name,Currency,Payment Method,Date,Description,Invoice ID,Transaction ID,Amount In,Fees,Amount Out");
        foreach (var r in result.Items)
        {
            sb.AppendLine($"{r.Id},{r.ClientId},\"{r.ClientName}\",{r.Currency},\"{r.PaymentMethod}\",{r.Date},\"{r.Description}\",{r.InvoiceId},{r.TransactionId},{r.AmountIn},{r.Fees},{r.AmountOut}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "transactions-report.csv");
    }

    /// <summary>Returns top clients ranked by transaction income.</summary>
    [HttpGet("top-clients-by-income")]
    public async Task<ActionResult<IReadOnlyList<TopClientByIncomeDto>>> GetTopClientsByIncomeAsync(
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<TopClientByIncomeDto>>(
            new TopClientsByIncomeQuery(take), ct);
        return Ok(result);
    }

    /// <summary>Returns client counts grouped by city and country.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("clients-by-city")]
    public async Task<ActionResult<IReadOnlyList<ClientsByCityDto>>> GetClientsByCityAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ClientsByCityDto>>(
            new ClientsByCityQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns client counts and revenue grouped by country.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("clients-by-country")]
    public async Task<ActionResult<IReadOnlyList<ClientsByCountryDto>>> GetClientsByCountryAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ClientsByCountryDto>>(
            new ClientsByCountryQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a lightweight list of clients for dropdown pickers.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("client-picker")]
    public async Task<ActionResult<IReadOnlyList<ClientPickerDto>>> GetClientPickerAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ClientPickerDto>>(
            new GetClientPickerQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns income by product grouped by product group for a given month.</summary>
    [HttpGet("income-by-product-grouped")]
    public async Task<ActionResult<IncomeByProductGroupedDto>> GetIncomeByProductGroupedAsync(
        [FromQuery] int? year, [FromQuery] int? month,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IncomeByProductGroupedDto>(
            new GetIncomeByProductGroupedQuery(year, month), ct);
        return Ok(result);
    }

    /// <summary>Returns monthly orders grouped by product group.</summary>
    [HttpGet("monthly-orders")]
    public async Task<ActionResult<MonthlyOrdersDto>> GetMonthlyOrdersAsync(
        [FromQuery] int? year, [FromQuery] int? month,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<MonthlyOrdersDto>(
            new GetMonthlyOrdersQuery(year, month), ct);
        return Ok(result);
    }

    /// <summary>Returns a filtered, paginated list of services for reporting.</summary>
    [HttpGet("services")]
    public async Task<ActionResult<ServiceReportResultDto>> GetServicesReportAsync(
        [FromQuery] string? status,
        [FromQuery] string? billingCycle,
        [FromQuery] DateOnly? createdFrom, [FromQuery] DateOnly? createdTo,
        [FromQuery] DateOnly? nextDueFrom, [FromQuery] DateOnly? nextDueTo,
        [FromQuery] DateOnly? terminatedFrom, [FromQuery] DateOnly? terminatedTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<ServiceReportResultDto>(new GetServicesReportQuery(
            status, billingCycle,
            createdFrom,
            createdTo,
            nextDueFrom,
            nextDueTo,
            terminatedFrom,
            terminatedTo,
            page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a filtered, paginated list of domains for reporting.</summary>
    [HttpGet("domains")]
    public async Task<ActionResult<DomainReportResultDto>> GetDomainsReportAsync(
        [FromQuery] string? status,
        [FromQuery] string? registrar,
        [FromQuery] DateOnly? registeredFrom, [FromQuery] DateOnly? registeredTo,
        [FromQuery] DateOnly? expiresFrom, [FromQuery] DateOnly? expiresTo,
        [FromQuery] DateOnly? nextDueFrom, [FromQuery] DateOnly? nextDueTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<DomainReportResultDto>(new GetDomainsReportQuery(
            status, registrar,
            registeredFrom,
            registeredTo,
            expiresFrom,
            expiresTo,
            nextDueFrom,
            nextDueTo,
            page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a filtered, paginated list of clients for reporting.</summary>
    [HttpGet("clients")]
    public async Task<ActionResult<ClientReportResultDto>> GetClientsReportAsync(
        [FromQuery] string? status,
        [FromQuery] string? country,
        [FromQuery] DateOnly? createdFrom, [FromQuery] DateOnly? createdTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<ClientReportResultDto>(new GetClientsReportQuery(
            status, country,
            createdFrom,
            createdTo,
            page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns credits issued to clients.</summary>
    [HttpGet("credits-reviewer")]
    public async Task<ActionResult<CreditsReviewerDto>> GetCreditsReviewerAsync(
        [FromQuery] int? clientId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] decimal? minAmount, [FromQuery] decimal? maxAmount,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<CreditsReviewerDto>(new GetCreditsReviewerQuery(
            clientId,
            from,
            to,
            minAmount, maxAmount), ct);
        return Ok(result);
    }

    /// <summary>Returns all suspended services.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("product-suspensions")]
    public async Task<ActionResult<IReadOnlyList<ProductSuspensionRowDto>>> GetProductSuspensionsAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ProductSuspensionRowDto>>(
            new GetProductSuspensionsQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns support ticket replies per admin per day for a given month.</summary>
    [HttpGet("support-ticket-replies")]
    public async Task<ActionResult<SupportTicketRepliesDto>> GetSupportTicketRepliesAsync(
        [FromQuery] int? year, [FromQuery] int? month,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<SupportTicketRepliesDto>(
            new GetSupportTicketRepliesQuery(year, month), ct);
        return Ok(result);
    }

    /// <summary>Returns sales tax liability for the given date range.</summary>
    [HttpGet("sales-tax")]
    public async Task<ActionResult<SalesTaxReportDto>> GetSalesTaxAsync(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<SalesTaxReportDto>(new GetSalesTaxReportQuery(
            from,
            to), ct);
        return Ok(result);
    }

    /// <summary>Returns daily transaction aggregates for a given month.</summary>
    [HttpGet("daily-transactions")]
    public async Task<ActionResult<MonthlyTransactionsReportDto>> GetDailyTransactionsAsync(
        [FromQuery] int? year, [FromQuery] int? month,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<MonthlyTransactionsReportDto>(
            new GetDailyTransactionsQuery(year, month), ct);
        return Ok(result);
    }

    /// <summary>Returns cached disk and bandwidth usage stats grouped by server.</summary>
    [HttpGet("disk-usage")]
    public async Task<ActionResult<DiskUsageDto>> GetDiskUsageAsync(CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<DiskUsageDto>(new GetDiskUsageReportQuery(), ct);
        return Ok(result);
    }

    /// <summary>Triggers a fresh fetch from all servers and returns updated disk usage stats.</summary>
    [HttpPost("disk-usage/update")]
    public async Task<ActionResult<DiskUsageDto>> UpdateDiskUsageAsync(CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<DiskUsageDto>(new UpdateDiskUsageCommand(), ct);
        return Ok(result);
    }

    /// <summary>Returns unpaid invoices assigned to the Direct Debit payment method.</summary>
    [HttpGet("direct-debit")]
    public async Task<ActionResult<DirectDebitDto>> GetDirectDebitAsync(CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<DirectDebitDto>(new GetDirectDebitQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns average customer retention grouped by product group.</summary>
    [HttpGet("customer-retention")]
    public async Task<ActionResult<CustomerRetentionDto>> GetCustomerRetentionAsync(
        [FromQuery] bool includeActive = true,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<CustomerRetentionDto>(
            new GetCustomerRetentionQuery(includeActive), ct);
        return Ok(result);
    }

    /// <summary>Returns cached SSL monitoring results grouped by expiry bucket.</summary>
    [HttpGet("ssl-monitoring")]
    public async Task<ActionResult<SslMonitoringDto>> GetSslMonitoringAsync(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<SslMonitoringDto>(
            new GetSslMonitoringQuery(includeInactive), ct);
        return Ok(result);
    }

    /// <summary>Re-checks all domains and returns fresh SSL monitoring results.</summary>
    [HttpPost("ssl-monitoring/revalidate")]
    public async Task<ActionResult<SslMonitoringDto>> RevalidateSslAsync(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<SslMonitoringDto>(
            new RevalidateSslMonitoringCommand(includeInactive), ct);
        return Ok(result);
    }

    /// <summary>Returns domain renewal reminder emails with optional filters.</summary>
    [HttpGet("domain-renewal-emails")]
    public async Task<ActionResult<DomainRenewalEmailsDto>> GetDomainRenewalEmailsAsync(
        [FromQuery] int? clientId,
        [FromQuery] string? registrar,
        [FromQuery] string? domain,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<DomainRenewalEmailsDto>(new GetDomainRenewalEmailsQuery(
            clientId, registrar, domain,
            from,
            to), ct);
        return Ok(result);
    }

    /// <summary>Returns VAT MOSS settlement data for the given quarter.</summary>
    [HttpGet("vat-moss")]
    public async Task<ActionResult<VatMossDto>> GetVatMossAsync(
        [FromQuery] int? year, [FromQuery] int? quarter,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<VatMossDto>(new GetVatMossQuery(year, quarter), ct);
        return Ok(result);
    }

    /// <summary>Returns ticket feedback comments with optional filters.</summary>
    [HttpGet("ticket-feedback-comments")]
    public async Task<ActionResult<TicketFeedbackCommentsDto>> GetTicketFeedbackCommentsAsync(
        [FromQuery] string? staffName,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TicketFeedbackCommentsDto>(new GetTicketFeedbackCommentsQuery(
            staffName,
            from,
            to), ct);
        return Ok(result);
    }

    /// <summary>Returns per-staff average feedback scores.</summary>
    [HttpGet("ticket-feedback-scores")]
    public async Task<ActionResult<TicketFeedbackScoresDto>> GetTicketFeedbackScoresAsync(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TicketFeedbackScoresDto>(new GetTicketFeedbackScoresQuery(
            from,
            to), ct);
        return Ok(result);
    }

    /// <summary>Returns rated tickets for review.</summary>
    [HttpGet("ticket-ratings-reviewer")]
    public async Task<ActionResult<TicketRatingsReviewerDto>> GetTicketRatingsReviewerAsync(
        [FromQuery] int? minRating,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TicketRatingsReviewerDto>(new GetTicketRatingsReviewerQuery(
            minRating,
            from,
            to), ct);
        return Ok(result);
    }

    /// <summary>Returns ticket tags with usage counts.</summary>
    [HttpGet("ticket-tags")]
    public async Task<ActionResult<TicketTagsDto>> GetTicketTagsAsync(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TicketTagsDto>(new GetTicketTagsQuery(
            from,
            to), ct);
        return Ok(result);
    }

    /// <summary>Returns a client account statement.</summary>
    [HttpGet("client-statement")]
    public async Task<ActionResult<ClientStatementDto>> GetClientStatementAsync(
        [FromQuery] int clientId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<ClientStatementDto>(new ClientStatementQuery(
            clientId,
            from,
            to), ct);
        return Ok(result);
    }
}
