namespace Innovayse.API.Reports;

using System.Text;
using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Queries.AgingInvoices;
using Innovayse.Application.Reports.Queries.AnnualIncome;
using Innovayse.Application.Reports.Queries.ClientsByCity;
using Innovayse.Application.Reports.Queries.ClientsByCountry;
using Innovayse.Application.Reports.Queries.ClientStatement;
using Innovayse.Application.Reports.Queries.DailyPerformance;
using Innovayse.Application.Reports.Queries.InvoicesReport;
using Innovayse.Application.Reports.Queries.NewCustomers;
using Innovayse.Application.Reports.Queries.TopClientsByIncome;
using Innovayse.Application.Reports.Queries.TransactionsReport;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>Admin reporting endpoints.</summary>
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
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        var toDate = to is not null ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from is not null ? DateOnly.Parse(from) : toDate.AddDays(-29);
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

    /// <summary>Returns income forecast based on recurring billing cycles.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("income-forecast")]
    public async Task<IActionResult> GetIncomeForecastAsync(CancellationToken ct)
    {
        var monthly = await bus.InvokeAsync<IReadOnlyList<AnnualIncomeDto>>(
            new AnnualIncomeQuery(DateTime.UtcNow.Year), ct);

        var result = monthly.Select((r, i) => new
        {
            month = r.Month,
            monthly = r.Amount,
            quarterly = monthly.Skip(Math.Max(0, i - 2)).Take(3).Sum(x => x.Amount),
            semiAnnual = monthly.Skip(Math.Max(0, i - 5)).Take(6).Sum(x => x.Amount),
            annual = monthly.Sum(x => x.Amount),
            total = monthly.Take(i + 1).Sum(x => x.Amount),
        });

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
        [FromQuery] string? createdFrom, [FromQuery] string? createdTo,
        [FromQuery] string? dueFrom, [FromQuery] string? dueTo,
        [FromQuery] string? paidFrom, [FromQuery] string? paidTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<InvoiceReportResultDto>(new InvoicesReportQuery(
            status,
            createdFrom is not null ? DateOnly.Parse(createdFrom) : null,
            createdTo is not null ? DateOnly.Parse(createdTo) : null,
            dueFrom is not null ? DateOnly.Parse(dueFrom) : null,
            dueTo is not null ? DateOnly.Parse(dueTo) : null,
            paidFrom is not null ? DateOnly.Parse(paidFrom) : null,
            paidTo is not null ? DateOnly.Parse(paidTo) : null,
            page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Exports invoices report as CSV.</summary>
    [HttpGet("invoices/export")]
    public async Task<IActionResult> ExportInvoicesCsvAsync(
        [FromQuery] string? status,
        [FromQuery] string? createdFrom, [FromQuery] string? createdTo,
        [FromQuery] string? dueFrom, [FromQuery] string? dueTo,
        [FromQuery] string? paidFrom, [FromQuery] string? paidTo,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<InvoiceReportResultDto>(new InvoicesReportQuery(
            status,
            createdFrom is not null ? DateOnly.Parse(createdFrom) : null,
            createdTo is not null ? DateOnly.Parse(createdTo) : null,
            dueFrom is not null ? DateOnly.Parse(dueFrom) : null,
            dueTo is not null ? DateOnly.Parse(dueTo) : null,
            paidFrom is not null ? DateOnly.Parse(paidFrom) : null,
            paidTo is not null ? DateOnly.Parse(paidTo) : null,
            1, 10000), ct);

        var sb = new StringBuilder();
        sb.AppendLine("ID,Client ID,Client Name,Status,Created Date,Due Date,Date Paid,SubTotal,Credit,Tax,Tax Rate,Total,Payment Method,Notes");
        foreach (var r in result.Items)
            sb.AppendLine($"{r.Id},{r.ClientId},\"{r.ClientName}\",{r.Status},{r.CreatedDate},{r.DueDate},{r.DatePaid},{r.SubTotal},{r.Credit},{r.Tax},{r.TaxRate},{r.Total},\"{r.PaymentMethod}\",\"{r.Notes}\"");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "invoices-report.csv");
    }

    /// <summary>Returns a filtered, paginated list of transactions.</summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<TransactionReportResultDto>> GetTransactionsReportAsync(
        [FromQuery] string? dateFrom, [FromQuery] string? dateTo,
        [FromQuery] string? paymentMethod,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TransactionReportResultDto>(new TransactionsReportQuery(
            dateFrom is not null ? DateOnly.Parse(dateFrom) : null,
            dateTo is not null ? DateOnly.Parse(dateTo) : null,
            paymentMethod, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Exports transactions report as CSV.</summary>
    [HttpGet("transactions/export")]
    public async Task<IActionResult> ExportTransactionsCsvAsync(
        [FromQuery] string? dateFrom, [FromQuery] string? dateTo,
        [FromQuery] string? paymentMethod,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TransactionReportResultDto>(new TransactionsReportQuery(
            dateFrom is not null ? DateOnly.Parse(dateFrom) : null,
            dateTo is not null ? DateOnly.Parse(dateTo) : null,
            paymentMethod, 1, 10000), ct);

        var sb = new StringBuilder();
        sb.AppendLine("ID,Client ID,Client Name,Currency,Payment Method,Date,Description,Invoice ID,Transaction ID,Amount In,Fees,Amount Out");
        foreach (var r in result.Items)
            sb.AppendLine($"{r.Id},{r.ClientId},\"{r.ClientName}\",{r.Currency},\"{r.PaymentMethod}\",{r.Date},\"{r.Description}\",{r.InvoiceId},{r.TransactionId},{r.AmountIn},{r.Fees},{r.AmountOut}");

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
    [HttpGet("clients-by-city")]
    public async Task<ActionResult<IReadOnlyList<ClientsByCityDto>>> GetClientsByCityAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ClientsByCityDto>>(
            new ClientsByCityQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns client counts and revenue grouped by country.</summary>
    [HttpGet("clients-by-country")]
    public async Task<ActionResult<IReadOnlyList<ClientsByCountryDto>>> GetClientsByCountryAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ClientsByCountryDto>>(
            new ClientsByCountryQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a client account statement.</summary>
    [HttpGet("client-statement")]
    public async Task<ActionResult<ClientStatementDto>> GetClientStatementAsync(
        [FromQuery] int clientId,
        [FromQuery] string? from, [FromQuery] string? to,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<ClientStatementDto>(new ClientStatementQuery(
            clientId,
            from is not null ? DateOnly.Parse(from) : null,
            to is not null ? DateOnly.Parse(to) : null), ct);
        return Ok(result);
    }
}
