namespace Innovayse.Infrastructure.Reports;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;
using Innovayse.Domain.Billing;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IReportRepository"/>.</summary>
public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    private static readonly string[] MonthNames = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DailyPerformanceDto>> GetDailyPerformanceAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var newInvoices = await db.Invoices
            .Where(i => i.CreatedAt >= fromUtc && i.CreatedAt <= toUtc)
            .GroupBy(i => DateOnly.FromDateTime(i.CreatedAt.DateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var paidInvoices = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= fromUtc && i.CreatedAt <= toUtc)
            .GroupBy(i => DateOnly.FromDateTime(i.CreatedAt.DateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var orders = await db.Orders
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
            .GroupBy(o => DateOnly.FromDateTime(o.CreatedAt.DateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var cancellations = await db.CancellationRequests
            .Where(c => c.CreatedAt >= fromUtc && c.CreatedAt <= toUtc)
            .GroupBy(c => DateOnly.FromDateTime(c.CreatedAt.DateTime))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i));

        return days.Select(day => new DailyPerformanceDto(
            Date: day.ToString("yyyy-MM-dd"),
            CompletedOrders: orders.FirstOrDefault(x => x.Date == day)?.Count ?? 0,
            NewInvoices: newInvoices.FirstOrDefault(x => x.Date == day)?.Count ?? 0,
            PaidInvoices: paidInvoices.FirstOrDefault(x => x.Date == day)?.Count ?? 0,
            FailedGateways: 0,
            TicketReplies: 0,
            Cancellations: cancellations.FirstOrDefault(x => x.Date == day)?.Count ?? 0
        )).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AnnualIncomeDto>> GetAnnualIncomeAsync(int year, CancellationToken ct)
    {
        var monthly = await db.InvoiceTransactions
            .Where(t => t.Type == InvoiceTransactionType.Payment && t.Date.Year == year)
            .GroupBy(t => t.Date.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        return Enumerable.Range(1, 12).Select(m => new AnnualIncomeDto(
            Month: MonthNames[m - 1],
            Amount: monthly.FirstOrDefault(x => x.Month == m)?.Total ?? 0m
        )).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgingInvoiceDto>> GetAgingInvoicesAsync(CancellationToken ct)
    {
        var unpaidStatuses = new[] { InvoiceStatus.Unpaid, InvoiceStatus.Overdue };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await db.Invoices
            .Where(i => unpaidStatuses.Contains(i.Status))
            .Join(db.Clients, inv => inv.ClientId, c => c.Id,
                (inv, c) => new { inv, ClientName = c.FirstName + " " + c.LastName })
            .OrderBy(x => x.inv.DueDate)
            .ToListAsync(ct);

        return rows.Select(x =>
        {
            var due = DateOnly.FromDateTime(x.inv.DueDate.DateTime);
            var days = today.DayNumber - due.DayNumber;
            var bucket = days <= 30 ? "0-30" : days <= 60 ? "31-60" : days <= 90 ? "61-90" : "90+";
            return new AgingInvoiceDto(
                InvoiceId: x.inv.Id,
                Client: x.ClientName,
                Amount: x.inv.Total,
                DueDate: due.ToString("yyyy-MM-dd"),
                DaysOutstanding: Math.Max(0, days),
                Bucket: bucket);
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NewCustomerDto>> GetNewCustomersAsync(int year, CancellationToken ct)
    {
        var newClients = await db.Clients
            .Where(c => c.CreatedAt.Year == year)
            .GroupBy(c => c.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var orders = await db.Orders
            .Where(o => o.CreatedAt.Year == year)
            .GroupBy(o => o.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Enumerable.Range(1, 12).Select(m =>
        {
            var signups = newClients.FirstOrDefault(x => x.Month == m)?.Count ?? 0;
            var placed = orders.FirstOrDefault(x => x.Month == m)?.Count ?? 0;
            var rate = signups > 0 ? (double)placed / signups * 100 : 0;
            return new NewCustomerDto(
                Month: MonthNames[m - 1],
                NewSignups: signups,
                OrdersPlaced: placed,
                OrdersCompleted: placed,
                ConversionRate: Math.Round(rate, 1));
        }).ToList();
    }
}
