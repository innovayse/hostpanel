using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Reports.Commands.RevalidateSslMonitoring;
using Innovayse.Hostpanel.Application.Reports.Commands.UpdateDiskUsage;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Queries.AgingInvoices;
using Innovayse.Hostpanel.Application.Reports.Queries.AgingInvoicesSummary;
using Innovayse.Hostpanel.Application.Reports.Queries.AnnualIncome;
using Innovayse.Hostpanel.Application.Reports.Queries.ClientsByCity;
using Innovayse.Hostpanel.Application.Reports.Queries.ClientsByCountry;
using Innovayse.Hostpanel.Application.Reports.Queries.ClientStatement;
using Innovayse.Hostpanel.Application.Reports.Queries.DailyPerformance;
using Innovayse.Hostpanel.Application.Reports.Queries.ExportInvoicesReport;
using Innovayse.Hostpanel.Application.Reports.Queries.ExportTransactionsReport;
using Innovayse.Hostpanel.Application.Reports.Queries.GetClientPicker;
using Innovayse.Hostpanel.Application.Reports.Queries.GetClientsReport;
using Innovayse.Hostpanel.Application.Reports.Queries.GetCreditsReviewer;
using Innovayse.Hostpanel.Application.Reports.Queries.GetCustomerRetention;
using Innovayse.Hostpanel.Application.Reports.Queries.GetDailyTransactions;
using Innovayse.Hostpanel.Application.Reports.Queries.GetDirectDebit;
using Innovayse.Hostpanel.Application.Reports.Queries.GetDiskUsageReport;
using Innovayse.Hostpanel.Application.Reports.Queries.GetDomainRenewalEmails;
using Innovayse.Hostpanel.Application.Reports.Queries.GetDomainsReport;
using Innovayse.Hostpanel.Application.Reports.Queries.GetIncomeByProductGrouped;
using Innovayse.Hostpanel.Application.Reports.Queries.GetIncomeForecast;
using Innovayse.Hostpanel.Application.Reports.Queries.GetMonthlyOrders;
using Innovayse.Hostpanel.Application.Reports.Queries.GetProductSuspensions;
using Innovayse.Hostpanel.Application.Reports.Queries.GetSalesTaxReport;
using Innovayse.Hostpanel.Application.Reports.Queries.GetServicesReport;
using Innovayse.Hostpanel.Application.Reports.Queries.GetSslMonitoring;
using Innovayse.Hostpanel.Application.Reports.Queries.GetSupportTicketReplies;
using Innovayse.Hostpanel.Application.Reports.Queries.GetTicketFeedbackComments;
using Innovayse.Hostpanel.Application.Reports.Queries.GetTicketFeedbackScores;
using Innovayse.Hostpanel.Application.Reports.Queries.GetTicketRatingsReviewer;
using Innovayse.Hostpanel.Application.Reports.Queries.GetTicketTags;
using Innovayse.Hostpanel.Application.Reports.Queries.GetVatMoss;
using Innovayse.Hostpanel.Application.Reports.Queries.InvoicesReport;
using Innovayse.Hostpanel.Application.Reports.Queries.NewCustomers;
using Innovayse.Hostpanel.Application.Reports.Queries.TopClientsByIncome;
using Innovayse.Hostpanel.Application.Reports.Queries.TransactionsReport;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin reporting endpoints for the whole module. Every action binds, dispatches one message,
/// and returns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every date filter binds as <see cref="DateOnly"/>, never as a <see langword="string"/> the
/// action parses itself</b>: <c>DateOnly.Parse</c> throws <see cref="FormatException"/> on a bad
/// input, which answers 500 rather than the 400 a caller's mistake deserves. Binding a date
/// parameter as <c>DateOnly?</c> hands the parse to model binding, which answers 400 with the same
/// body a malformed <c>int</c> gets, so a new date filter here must stay <c>DateOnly?</c>.
/// </para>
/// </remarks>
[Route("api/reports")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ReportingController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ReportingController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns daily order, invoice, ticket and cancellation counts for a date range.</summary>
    /// <param name="query">The date range; each end defaults so the range is the trailing 30 days.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with one row per day; 400 when the range is invalid; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("daily-performance")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyPerformanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDailyPerformanceAsync([FromQuery] DailyPerformanceQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<DailyPerformanceDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns monthly paid-invoice income totals for a year.</summary>
    /// <param name="query">The year; defaults to the current UTC year.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with twelve rows, January first; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("annual-income")]
    [ProducesResponseType(typeof(IReadOnlyList<AnnualIncomeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAnnualIncomeAsync([FromQuery] AnnualIncomeQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<AnnualIncomeDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns one page of the open invoices that still owe money, with their aging period.</summary>
    /// <param name="query">The page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page, oldest due date first, each amount due in its invoice currency; 400 when the page or page size is out of range; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("aging-invoices")]
    [ProducesResponseType(typeof(PaginatedList<AgingInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAgingInvoicesAsync([FromQuery] AgingInvoicesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<AgingInvoiceDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns aging invoices summed by period and currency.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the per-period, per-currency totals; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("aging-invoices-summary")]
    [ProducesResponseType(typeof(AgingInvoiceSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAgingInvoicesSummaryAsync([FromQuery] AgingInvoicesSummaryQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<AgingInvoiceSummaryDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns an income forecast projected from recurring billing cycles.</summary>
    /// <param name="query">The year; defaults to the current UTC year.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with one row per month; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("income-forecast")]
    [ProducesResponseType(typeof(IReadOnlyList<IncomeForecastRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIncomeForecastAsync([FromQuery] GetIncomeForecastQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<IncomeForecastRowDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single client's account statement.</summary>
    /// <param name="query">The client and date range to statement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the statement; 404 when the client id names nobody; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("client-statement")]
    [ProducesResponseType(typeof(ClientStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClientStatementAsync([FromQuery] ClientStatementQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ClientStatementDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns client counts grouped by city and country.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the grouped counts, largest first; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("clients-by-city")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientsByCityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClientsByCityAsync([FromQuery] ClientsByCityQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ClientsByCityDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns client counts and paid-invoice revenue grouped by country.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the grouped counts, largest first; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("clients-by-country")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientsByCountryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClientsByCountryAsync([FromQuery] ClientsByCountryQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ClientsByCountryDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a lightweight client list for dropdown pickers.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with every client's id and name; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("client-picker")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientPickerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClientPickerAsync([FromQuery] GetClientPickerQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ClientPickerDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns income by product, grouped by product group, for one month.</summary>
    /// <param name="query">The month; defaults to the current UTC year and month.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the grouped totals; 400 when the month is out of range; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("income-by-product-grouped")]
    [ProducesResponseType(typeof(IncomeByProductGroupedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIncomeByProductGroupedAsync(
        [FromQuery] GetIncomeByProductGroupedQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IncomeByProductGroupedDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a filtered, paginated list of clients for reporting.</summary>
    /// <param name="query">The filters and page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page and total; 400 when the page or page size is invalid; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("clients")]
    [ProducesResponseType(typeof(PaginatedList<ClientReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClientsReportAsync([FromQuery] GetClientsReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<ClientReportDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns credits issued to clients, optionally filtered.</summary>
    /// <param name="query">The filters and the page to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with one page of the matching credits and their totals per currency; 400 when a filter, the page or the page size is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("credits-reviewer")]
    [ProducesResponseType(typeof(CreditsReviewerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCreditsReviewerAsync([FromQuery] GetCreditsReviewerQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<CreditsReviewerDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a filtered, paginated list of domains for reporting.</summary>
    /// <param name="query">The filters and page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page and total; 400 when the page or page size is invalid; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("domains")]
    [ProducesResponseType(typeof(PaginatedList<DomainReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDomainsReportAsync([FromQuery] GetDomainsReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<DomainReportDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns unpaid invoices assigned to the Direct Debit payment method.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with every matching invoice, soonest due first; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("direct-debit")]
    [ProducesResponseType(typeof(DirectDebitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDirectDebitAsync([FromQuery] GetDirectDebitQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DirectDebitDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns average customer retention grouped by product group.</summary>
    /// <param name="query">Whether services and domains still running count towards the average.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the average retention per product; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("customer-retention")]
    [ProducesResponseType(typeof(CustomerRetentionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCustomerRetentionAsync([FromQuery] GetCustomerRetentionQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<CustomerRetentionDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns daily transaction aggregates for one month.</summary>
    /// <param name="query">The month; defaults to the current UTC year and month.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with one row per day of the month; 400 when the month is out of range; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("daily-transactions")]
    [ProducesResponseType(typeof(MonthlyTransactionsReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDailyTransactionsAsync([FromQuery] GetDailyTransactionsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MonthlyTransactionsReportDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the cached disk and bandwidth usage figures, grouped by server.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the cached report; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("disk-usage")]
    [ProducesResponseType(typeof(DiskUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDiskUsageAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DiskUsageDto>>(new GetDiskUsageReportQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Fetches fresh figures from every hosting server and replaces the cache.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the refreshed report; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpPost("disk-usage/update")]
    [ProducesResponseType(typeof(DiskUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateDiskUsageAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DiskUsageDto>>(new UpdateDiskUsageCommand(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns domain renewal reminder emails, optionally filtered.</summary>
    /// <param name="query">The filters to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with every matching reminder, most recently sent first; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("domain-renewal-emails")]
    [ProducesResponseType(typeof(DomainRenewalEmailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDomainRenewalEmailsAsync(
        [FromQuery] GetDomainRenewalEmailsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DomainRenewalEmailsDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Re-checks every domain's certificate and replaces the cached SSL monitoring report.</summary>
    /// <param name="includeInactive">Whether domains that are no longer active are checked too.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the refreshed report; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpPost("ssl-monitoring/revalidate")]
    [ProducesResponseType(typeof(SslMonitoringDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevalidateSslMonitoringAsync(
        [FromQuery] bool includeInactive, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SslMonitoringDto>>(
            new RevalidateSslMonitoringCommand(includeInactive), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns monthly new customer and order metrics.</summary>
    /// <param name="query">The year; defaults to the current UTC year.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with twelve rows, January first; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("new-customers")]
    [ProducesResponseType(typeof(IReadOnlyList<NewCustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNewCustomersAsync([FromQuery] NewCustomersQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<NewCustomerDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a filtered, paginated list of invoices.</summary>
    /// <param name="query">The filters and page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page and total; 400 when the page or page size is invalid; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(PaginatedList<InvoiceReportRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvoicesReportAsync([FromQuery] InvoicesReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<InvoiceReportRowDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Exports the Invoices report as a CSV file.</summary>
    /// <param name="query">The filters to apply; every matching row up to the export's row ceiling is written.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the CSV download; 400 when a filter is not a valid value; 401 without a session; 403 for a caller without access; 429 when the rate limit is hit.</returns>
    [HttpGet("invoices/export")]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportInvoicesReportAsync(
        [FromQuery] ExportInvoicesReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<FileDownloadDto>>(query, ct);
        return FileResult(outcome);
    }

    /// <summary>Returns a filtered, paginated list of transactions.</summary>
    /// <param name="query">The filters and page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page and total; 400 when the page or page size is invalid; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PaginatedList<TransactionReportRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTransactionsReportAsync(
        [FromQuery] TransactionsReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<TransactionReportRowDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Exports the Transactions report as a CSV file.</summary>
    /// <param name="query">The filters to apply; every matching row up to the export's row ceiling is written.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the CSV download; 400 when a filter is not a valid value; 401 without a session; 403 for a caller without access; 429 when the rate limit is hit.</returns>
    [HttpGet("transactions/export")]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportTransactionsReportAsync(
        [FromQuery] ExportTransactionsReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<FileDownloadDto>>(query, ct);
        return FileResult(outcome);
    }

    /// <summary>Returns top clients ranked by transaction income.</summary>
    /// <param name="query">How many clients to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the ranked clients; 400 when the count is out of range; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("top-clients-by-income")]
    [ProducesResponseType(typeof(IReadOnlyList<TopClientByIncomeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTopClientsByIncomeAsync(
        [FromQuery] TopClientsByIncomeQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<TopClientByIncomeDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns monthly orders grouped by product group.</summary>
    /// <param name="query">Year and month to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the grouped orders and totals; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("monthly-orders")]
    [ProducesResponseType(typeof(MonthlyOrdersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyOrdersAsync([FromQuery] GetMonthlyOrdersQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MonthlyOrdersDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a filtered, paginated list of services for reporting.</summary>
    /// <param name="query">The filters and page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page and total; 400 when the page or page size is invalid; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("services")]
    [ProducesResponseType(typeof(PaginatedList<ServiceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetServicesReportAsync(
        [FromQuery] GetServicesReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<ServiceReportDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns all suspended services.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with one row per suspended service; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("product-suspensions")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductSuspensionRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProductSuspensionsAsync(
        [FromQuery] GetProductSuspensionsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ProductSuspensionRowDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns support ticket replies per admin per day for a given month.</summary>
    /// <param name="query">Year and month to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the reply counts; 400 when the month is out of range; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("support-ticket-replies")]
    [ProducesResponseType(typeof(SupportTicketRepliesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSupportTicketRepliesAsync(
        [FromQuery] GetSupportTicketRepliesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SupportTicketRepliesDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns sales tax liability for the invoices paid in the given date range.</summary>
    /// <param name="query">The payment days to report on, both bounds optional, and the page of rows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the totals per currency and one page of the invoices; 400 when the page or page size is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("sales-tax")]
    [ProducesResponseType(typeof(SalesTaxReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSalesTaxAsync([FromQuery] GetSalesTaxReportQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SalesTaxReportDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns cached SSL monitoring results grouped by expiry bucket.</summary>
    /// <param name="query">Whether inactive domains are included.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the cached report; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("ssl-monitoring")]
    [ProducesResponseType(typeof(SslMonitoringDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSslMonitoringAsync([FromQuery] GetSslMonitoringQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SslMonitoringDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns VAT MOSS settlement data for the given quarter.</summary>
    /// <param name="query">Year and quarter to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the quarter's bounds and its rows per member state, VAT rate and invoice currency; 400 when the quarter is out of range; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("vat-moss")]
    [ProducesResponseType(typeof(VatMossDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVatMossAsync([FromQuery] GetVatMossQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<VatMossDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns ticket feedback comments with optional filters.</summary>
    /// <param name="query">Admin and date range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the matching feedback; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("ticket-feedback-comments")]
    [ProducesResponseType(typeof(TicketFeedbackCommentsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketFeedbackCommentsAsync(
        [FromQuery] GetTicketFeedbackCommentsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TicketFeedbackCommentsDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns per-staff average feedback scores.</summary>
    /// <param name="query">The date range to report on; both bounds are optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with one row per admin; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("ticket-feedback-scores")]
    [ProducesResponseType(typeof(TicketFeedbackScoresDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketFeedbackScoresAsync(
        [FromQuery] GetTicketFeedbackScoresQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TicketFeedbackScoresDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns rated tickets for review.</summary>
    /// <param name="query">Minimum rating and date range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the matching rated tickets; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("ticket-ratings-reviewer")]
    [ProducesResponseType(typeof(TicketRatingsReviewerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketRatingsReviewerAsync(
        [FromQuery] GetTicketRatingsReviewerQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TicketRatingsReviewerDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns ticket tags with usage counts.</summary>
    /// <param name="query">The date range to report on; both bounds are optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with one row per distinct tag; 400 when a filter is not a valid value; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet("ticket-tags")]
    [ProducesResponseType(typeof(TicketTagsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketTagsAsync([FromQuery] GetTicketTagsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TicketTagsDto>>(query, ct);
        return HandleResult(outcome);
    }
}
