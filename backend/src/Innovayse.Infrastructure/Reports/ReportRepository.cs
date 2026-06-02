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
            .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month, i.CreatedAt.Day })
            .Select(g => new { Date = new { g.Key.Year, g.Key.Month, g.Key.Day }, Count = g.Count() })
            .ToListAsync(ct);

        var paidInvoices = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= fromUtc && i.CreatedAt <= toUtc)
            .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month, i.CreatedAt.Day })
            .Select(g => new { Date = new { g.Key.Year, g.Key.Month, g.Key.Day }, Count = g.Count() })
            .ToListAsync(ct);

        var orders = await db.Orders
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
            .Select(g => new { Date = new { g.Key.Year, g.Key.Month, g.Key.Day }, Count = g.Count() })
            .ToListAsync(ct);

        var cancellations = await db.CancellationRequests
            .Where(c => c.CreatedAt >= fromUtc && c.CreatedAt <= toUtc)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month, c.CreatedAt.Day })
            .Select(g => new { Date = new { g.Key.Year, g.Key.Month, g.Key.Day }, Count = g.Count() })
            .ToListAsync(ct);

        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i));

        return days.Select(day => new DailyPerformanceDto(
            Date: day.ToString("yyyy-MM-dd"),
            CompletedOrders: orders.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0,
            NewInvoices: newInvoices.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0,
            PaidInvoices: paidInvoices.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0,
            FailedGateways: 0,
            TicketReplies: 0,
            Cancellations: cancellations.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0
        )).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AnnualIncomeDto>> GetAnnualIncomeAsync(int year, CancellationToken ct)
    {
        var monthly = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt.Year == year)
            .GroupBy(i => i.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(i => i.Total) })
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MonthlyTransactionDto>> GetMonthlyTransactionsAsync(int year, CancellationToken ct)
    {
        var grouped = await db.InvoiceTransactions
            .Where(t => t.Date.Year == year)
            .GroupBy(t => t.Date.Month)
            .Select(g => new
            {
                Month = g.Key,
                Count = g.Count(),
                TotalIn = g.Where(t => t.Type == InvoiceTransactionType.Payment).Sum(t => t.Amount),
                TotalOut = g.Where(t => t.Type == InvoiceTransactionType.Refund).Sum(t => t.Amount),
                TotalFees = g.Sum(t => t.Fees),
            })
            .ToListAsync(ct);

        return Enumerable.Range(1, 12).Select(m =>
        {
            var row = grouped.FirstOrDefault(x => x.Month == m);
            return new MonthlyTransactionDto(
                MonthNames[m - 1],
                row?.Count ?? 0,
                row?.TotalIn ?? 0m,
                row?.TotalOut ?? 0m,
                row?.TotalFees ?? 0m);
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TopClientDto>> GetTopClientsAsync(int take, CancellationToken ct)
    {
        var grouped = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid)
            .GroupBy(i => i.ClientId)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(i => i.Total), Count = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(take)
            .Join(db.Clients, x => x.ClientId, c => c.Id,
                (x, c) => new { x.ClientId, Name = c.FirstName + " " + c.LastName, x.Total, x.Count })
            .ToListAsync(ct);

        return grouped.Select((x, i) => new TopClientDto(
            Rank: i + 1,
            ClientId: x.ClientId,
            ClientName: x.Name,
            TotalIncome: x.Total,
            InvoiceCount: x.Count,
            AvgInvoice: x.Count > 0 ? Math.Round(x.Total / x.Count, 2) : 0m)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IncomeByProductDto>> GetIncomeByProductAsync(CancellationToken ct)
    {
        var grouped = await db.InvoiceItems
            .Join(db.Invoices.Where(i => i.Status == InvoiceStatus.Paid),
                item => item.InvoiceId, inv => inv.Id, (item, inv) => item)
            .GroupBy(item => item.Description)
            .Select(g => new
            {
                Product = g.Key,
                Units = g.Sum(x => x.Quantity),
                Total = g.Sum(x => x.UnitPrice * x.Quantity),
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        var grandTotal = grouped.Sum(x => x.Total);

        return grouped.Select(x => new IncomeByProductDto(
            Product: x.Product,
            UnitsSold: x.Units,
            TotalIncome: x.Total,
            Percentage: grandTotal > 0 ? Math.Round((double)(x.Total / grandTotal) * 100, 1) : 0)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientsByCountryDto>> GetClientsByCountryAsync(CancellationToken ct)
    {
        var counts = await db.Clients
            .GroupBy(c => c.Country)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var revenue = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid)
            .Join(db.Clients, i => i.ClientId, c => c.Id, (i, c) => new { c.Country, i.Total })
            .GroupBy(x => x.Country)
            .Select(g => new { Country = g.Key, Total = g.Sum(x => x.Total) })
            .ToListAsync(ct);

        return counts
            .OrderByDescending(x => x.Count)
            .Select(x => new ClientsByCountryDto(
                Country: string.IsNullOrWhiteSpace(x.Country) ? "Unknown" : x.Country!,
                ClientCount: x.Count,
                TotalRevenue: revenue.FirstOrDefault(r => r.Country == x.Country)?.Total ?? 0m))
            .ToList();
    }
}
