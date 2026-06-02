namespace Innovayse.API.Reports;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Queries.AgingInvoices;
using Innovayse.Application.Reports.Queries.AnnualIncome;
using Innovayse.Application.Reports.Queries.DailyPerformance;
using Innovayse.Application.Reports.Queries.NewCustomers;
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
}
