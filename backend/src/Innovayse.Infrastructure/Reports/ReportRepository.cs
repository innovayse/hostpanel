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

        var openedTickets = await db.Tickets
            .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt <= toUtc)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month, t.CreatedAt.Day })
            .Select(g => new { Date = new { g.Key.Year, g.Key.Month, g.Key.Day }, Count = g.Count() })
            .ToListAsync(ct);

        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i));

        return days.Select(day => new DailyPerformanceDto(
            Date: day.ToString("yyyy-MM-dd"),
            CompletedOrders: orders.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0,
            NewInvoices: newInvoices.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0,
            PaidInvoices: paidInvoices.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0,
            OpenedTickets: openedTickets.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0,
            TicketReplies: 0,
            CancellationRequests: cancellations.FirstOrDefault(x => x.Date.Year == day.Year && x.Date.Month == day.Month && x.Date.Day == day.Day)?.Count ?? 0
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
    public async Task<AgingInvoiceSummaryDto> GetAgingInvoicesSummaryAsync(CancellationToken ct)
    {
        var unpaidStatuses = new[] { InvoiceStatus.Unpaid, InvoiceStatus.Overdue };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var invoices = await db.Invoices
            .Where(i => unpaidStatuses.Contains(i.Status))
            .Join(db.Clients, inv => inv.ClientId, c => c.Id,
                (inv, c) => new { inv.Total, inv.DueDate, Currency = c.Currency ?? "USD" })
            .ToListAsync(ct);

        var periodNames = new[] { "0 - 30", "30 - 60", "60 - 90", "90 - 120", "120 +" };
        // Always show standard currencies even if no invoices exist for them
        var defaultCurrencies = new[] { "USD", "EUR", "AMD", "RUB" };
        var invoiceCurrencies = invoices.Select(x => x.Currency).Distinct();
        var currencies = defaultCurrencies.Union(invoiceCurrencies).Distinct().OrderBy(c => c).ToList();

        var periods = new List<AgingPeriodDto>();
        var totals = new Dictionary<string, decimal>();
        foreach (var c in currencies) totals[c] = 0m;

        foreach (var periodName in periodNames)
        {
            var amounts = new Dictionary<string, decimal>();
            foreach (var c in currencies) amounts[c] = 0m;

            foreach (var inv in invoices)
            {
                var due = DateOnly.FromDateTime(inv.DueDate.DateTime);
                var days = today.DayNumber - due.DayNumber;
                var bucket = days <= 30 ? "0 - 30" : days <= 60 ? "30 - 60" : days <= 90 ? "60 - 90" : days <= 120 ? "90 - 120" : "120 +";
                if (bucket != periodName) continue;
                amounts[inv.Currency] += inv.Total;
                totals[inv.Currency] += inv.Total;
            }

            periods.Add(new AgingPeriodDto(periodName, amounts));
        }

        return new AgingInvoiceSummaryDto(periods, totals, currencies);
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientsByCityDto>> GetClientsByCityAsync(CancellationToken ct)
    {
        var clients = await db.Clients
            .Where(c => c.City != null && c.City != "" && c.Country != null && c.Country != "")
            .Select(c => new { c.City, c.Country })
            .ToListAsync(ct);

        return clients
            .GroupBy(c => new { c.City, c.Country })
            .Select(g => new ClientsByCityDto(g.Key.City!, g.Key.Country!, g.Count()))
            .OrderByDescending(x => x.ClientCount)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<InvoiceReportResultDto> GetInvoicesReportAsync(
        string? status, DateOnly? createdFrom, DateOnly? createdTo,
        DateOnly? dueFrom, DateOnly? dueTo, DateOnly? paidFrom, DateOnly? paidTo,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Invoices
            .Join(db.Clients, i => i.ClientId, c => c.Id,
                (i, c) => new { i, ClientName = c.FirstName + " " + c.LastName });

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var parsed))
            query = query.Where(x => x.i.Status == parsed);

        if (createdFrom.HasValue)
            query = query.Where(x => x.i.CreatedAt >= createdFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (createdTo.HasValue)
            query = query.Where(x => x.i.CreatedAt <= createdTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (dueFrom.HasValue)
            query = query.Where(x => x.i.DueDate >= dueFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (dueTo.HasValue)
            query = query.Where(x => x.i.DueDate <= dueTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (paidFrom.HasValue)
            query = query.Where(x => x.i.PaidAt != null && x.i.PaidAt >= paidFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (paidTo.HasValue)
            query = query.Where(x => x.i.PaidAt != null && x.i.PaidAt <= paidTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        var totalCount = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(x => x.i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InvoiceReportRowDto(
                x.i.Id,
                x.i.ClientId,
                x.ClientName,
                null,
                x.i.CreatedAt.ToString("yyyy-MM-dd"),
                x.i.DueDate.ToString("yyyy-MM-dd"),
                x.i.PaidAt != null ? x.i.PaidAt.Value.ToString("yyyy-MM-dd") : null,
                null,
                x.i.Status == InvoiceStatus.Cancelled ? x.i.CreatedAt.ToString("yyyy-MM-dd") : null,
                x.i.SubTotal,
                x.i.Credit,
                x.i.Tax,
                x.i.TaxRate,
                x.i.Total,
                x.i.Status.ToString(),
                x.i.PaymentMethod,
                x.i.Notes))
            .ToListAsync(ct);

        return new InvoiceReportResultDto(rows, totalCount);
    }

    /// <inheritdoc/>
    public async Task<TransactionReportResultDto> GetTransactionsReportAsync(
        DateOnly? dateFrom, DateOnly? dateTo, string? paymentMethod,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Transactions
            .Join(db.Clients, t => t.ClientId, c => c.Id,
                (t, c) => new { t, ClientName = c.FirstName + " " + c.LastName });

        if (dateFrom.HasValue)
            query = query.Where(x => x.t.Date >= dateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (dateTo.HasValue)
            query = query.Where(x => x.t.Date <= dateTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(paymentMethod))
            query = query.Where(x => x.t.PaymentMethod == paymentMethod);

        var totalCount = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(x => x.t.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TransactionReportRowDto(
                x.t.Id,
                x.t.ClientId,
                x.ClientName,
                "USD",
                x.t.PaymentMethod,
                x.t.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                x.t.Description,
                null,
                x.t.TransactionId,
                x.t.AmountIn,
                x.t.Fees,
                x.t.AmountOut))
            .ToListAsync(ct);

        return new TransactionReportResultDto(rows, totalCount);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TopClientByIncomeDto>> GetTopClientsByIncomeAsync(int take, CancellationToken ct)
    {
        var grouped = await db.Transactions
            .GroupBy(t => t.ClientId)
            .Select(g => new
            {
                ClientId = g.Key,
                TotalIn = g.Sum(t => t.AmountIn),
                TotalFees = g.Sum(t => t.Fees),
                TotalOut = g.Sum(t => t.AmountOut),
            })
            .OrderByDescending(x => x.TotalIn)
            .Take(take)
            .Join(db.Clients, x => x.ClientId, c => c.Id,
                (x, c) => new TopClientByIncomeDto(
                    x.ClientId,
                    c.FirstName + " " + c.LastName,
                    x.TotalIn,
                    x.TotalFees,
                    x.TotalOut,
                    x.TotalIn - x.TotalOut - x.TotalFees))
            .ToListAsync(ct);

        return grouped;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientPickerDto>> GetClientPickerListAsync(CancellationToken ct)
    {
        return await db.Clients
            .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
            .Select(c => new ClientPickerDto(c.Id, c.FirstName + " " + c.LastName))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<ClientStatementDto> GetClientStatementAsync(int clientId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var fromDate = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            ?? DateTimeOffset.MinValue;
        var toDate = to?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
            ?? DateTimeOffset.UtcNow;

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new InvalidOperationException($"Client {clientId} not found.");
        var clientName = $"{client.FirstName} {client.LastName}";

        // Previous balance: sum of all invoices & transactions before the period
        var prevInvoiceDebit = await db.Invoices
            .Where(i => i.ClientId == clientId && i.CreatedAt < fromDate)
            .SumAsync(i => i.Total, ct);

        var prevTxCredit = await db.Transactions
            .Where(t => t.ClientId == clientId && t.Date < fromDate)
            .SumAsync(t => t.AmountIn - t.AmountOut, ct);

        var previousBalance = prevInvoiceDebit - prevTxCredit;

        // Invoices in range
        var invoices = await db.Invoices
            .Where(i => i.ClientId == clientId && i.CreatedAt >= fromDate && i.CreatedAt <= toDate)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new { i.Id, i.CreatedAt, i.Total })
            .ToListAsync(ct);

        // Transactions in range
        var transactions = await db.Transactions
            .Where(t => t.ClientId == clientId && t.Date >= fromDate && t.Date <= toDate)
            .OrderBy(t => t.Date)
            .Select(t => new { t.Id, t.Date, t.Description, t.AmountIn, t.AmountOut })
            .ToListAsync(ct);

        // Merge and sort
        var lines = new List<(DateTimeOffset Date, string Type, string Desc, decimal Debit, decimal Credit)>();

        foreach (var inv in invoices)
            lines.Add((inv.CreatedAt, "Invoice", $"Invoice Payment - #{inv.Id}", inv.Total, 0m));

        foreach (var tx in transactions)
            lines.Add((tx.Date, "Transaction", tx.Description, tx.AmountOut, tx.AmountIn));

        lines.Sort((a, b) => a.Date.CompareTo(b.Date));

        // Build running balance
        var running = previousBalance;
        var result = new List<ClientStatementLineDto>();
        foreach (var line in lines)
        {
            running = running + line.Debit - line.Credit;
            result.Add(new ClientStatementLineDto(
                line.Type,
                line.Date.ToString("yyyy-MM-dd"),
                line.Desc,
                line.Debit,
                line.Credit,
                running));
        }

        return new ClientStatementDto(clientId, clientName, previousBalance, result, running);
    }

    /// <inheritdoc/>
    public async Task<IncomeByProductGroupedDto> GetIncomeByProductGroupedAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        // Income from paid invoices this month matched by product name
        var paidItems = await db.InvoiceItems
            .Join(db.Invoices.Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= from && i.CreatedAt < to),
                item => item.InvoiceId, inv => inv.Id, (item, inv) => item)
            .GroupBy(item => item.Description)
            .Select(g => new { Name = g.Key, Units = g.Sum(x => x.Quantity), Income = g.Sum(x => x.UnitPrice * x.Quantity) })
            .ToListAsync(ct);

        var incomeMap = paidItems.ToDictionary(x => x.Name, x => new { x.Units, x.Income });

        var groups = await db.ProductGroups
            .Where(g => g.IsActive)
            .Include(g => g.Products.Where(p => p.Status == Domain.Products.ProductStatus.Active))
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        var result = groups.Select(g =>
        {
            var products = g.Products.Select(p =>
            {
                var sale = incomeMap.GetValueOrDefault(p.Name);
                return new IncomeByProductRowDto(p.Id, p.Name, sale?.Units ?? 0, sale?.Income ?? 0m);
            }).ToList();

            return new IncomeByProductGroupDto(
                g.Name,
                products,
                products.Sum(p => p.UnitsSold),
                products.Sum(p => p.TotalIncome));
        }).ToList();

        return new IncomeByProductGroupedDto(
            month, year, result,
            result.Sum(g => g.GroupUnitsSold),
            result.Sum(g => g.GroupIncome));
    }

    /// <inheritdoc/>
    public async Task<MonthlyOrdersDto> GetMonthlyOrdersAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        // Get all orders in this month with their items
        var orderItems = await db.Orders
            .Where(o => o.CreatedAt >= from && o.CreatedAt < to && o.Status == Domain.Orders.OrderStatus.Active)
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Count(), Value = g.Sum(i => i.FirstPaymentAmount) })
            .ToListAsync(ct);

        var salesMap = orderItems.ToDictionary(x => x.ProductId, x => new { x.Units, x.Value });

        // Get all product groups with their products
        var groups = await db.ProductGroups
            .Where(g => g.IsActive)
            .Include(g => g.Products.Where(p => p.Status == Domain.Products.ProductStatus.Active))
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        var result = groups.Select(g =>
        {
            var products = g.Products.Select(p =>
            {
                var sale = salesMap.GetValueOrDefault(p.Id);
                return new MonthlyOrderProductDto(p.Id, p.Name, sale?.Units ?? 0, sale?.Value ?? 0m);
            }).ToList();

            return new MonthlyOrderGroupDto(
                g.Name,
                products,
                products.Sum(p => p.UnitsSold),
                products.Sum(p => p.Value));
        }).ToList();

        return new MonthlyOrdersDto(
            month, year, result,
            result.Sum(g => g.GroupUnitsSold),
            result.Sum(g => g.GroupValue));
    }

    /// <inheritdoc/>
    public async Task<ServiceReportResultDto> GetServicesReportAsync(
        string? status, string? billingCycle,
        DateOnly? createdFrom, DateOnly? createdTo,
        DateOnly? nextDueFrom, DateOnly? nextDueTo,
        DateOnly? terminatedFrom, DateOnly? terminatedTo,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.ClientServices.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status.ToString() == status);
        if (!string.IsNullOrWhiteSpace(billingCycle))
            query = query.Where(s => s.BillingCycle.ToLower() == billingCycle.ToLower());
        if (createdFrom.HasValue)
            query = query.Where(s => s.CreatedAt >= createdFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (createdTo.HasValue)
            query = query.Where(s => s.CreatedAt <= createdTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (nextDueFrom.HasValue)
            query = query.Where(s => s.NextRenewalAt >= nextDueFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (nextDueTo.HasValue)
            query = query.Where(s => s.NextRenewalAt <= nextDueTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (terminatedFrom.HasValue)
            query = query.Where(s => s.TerminatedAt >= terminatedFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (terminatedTo.HasValue)
            query = query.Where(s => s.TerminatedAt <= terminatedTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        var totalCount = await query.CountAsync(ct);

        var clientIds = await query.Select(s => s.ClientId).Distinct().ToListAsync(ct);
        var clientNames = await db.Clients
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, Name = c.FirstName + " " + c.LastName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var productIds = await query.Select(s => s.ProductId).Distinct().ToListAsync(ct);
        var productNames = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = items.Select(s => new ServiceReportDto(
            s.Id,
            s.ClientId,
            clientNames.GetValueOrDefault(s.ClientId, ""),
            s.ProductId,
            productNames.GetValueOrDefault(s.ProductId, ""),
            s.Domain,
            s.BillingCycle,
            s.FirstPaymentAmount,
            s.RecurringAmount,
            s.PaymentMethod,
            s.CreatedAt.ToString("yyyy-MM-dd"),
            s.NextRenewalAt?.ToString("yyyy-MM-dd"),
            s.TerminatedAt?.ToString("yyyy-MM-dd"),
            s.Status.ToString(),
            s.AdminNotes)).ToList();

        return new ServiceReportResultDto(result, totalCount);
    }

    /// <inheritdoc/>
    public async Task<DomainReportResultDto> GetDomainsReportAsync(
        string? status, string? registrar,
        DateOnly? registeredFrom, DateOnly? registeredTo,
        DateOnly? expiresFrom, DateOnly? expiresTo,
        DateOnly? nextDueFrom, DateOnly? nextDueTo,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Domains.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status.ToString() == status);
        if (!string.IsNullOrWhiteSpace(registrar))
            query = query.Where(d => d.Registrar != null && d.Registrar.ToLower().Contains(registrar.ToLower()));
        if (registeredFrom.HasValue)
            query = query.Where(d => d.RegisteredAt >= registeredFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (registeredTo.HasValue)
            query = query.Where(d => d.RegisteredAt <= registeredTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (expiresFrom.HasValue)
            query = query.Where(d => d.ExpiresAt >= expiresFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (expiresTo.HasValue)
            query = query.Where(d => d.ExpiresAt <= expiresTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (nextDueFrom.HasValue)
            query = query.Where(d => d.NextDueDate >= nextDueFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (nextDueTo.HasValue)
            query = query.Where(d => d.NextDueDate <= nextDueTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        var totalCount = await query.CountAsync(ct);

        var clientIds = await query.Select(d => d.ClientId).Distinct().ToListAsync(ct);
        var clientNames = await db.Clients
            .Where(c => clientIds.Contains(c.Id))
            .Join(db.Users, c => c.UserId, u => u.Id,
                (c, u) => new { c.Id, Name = c.FirstName + " " + c.LastName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = await query
            .OrderByDescending(d => d.RegisteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id, d.ClientId, d.OrderId, d.OrderType,
                d.Name, d.Tld, d.FirstPaymentAmount, d.RecurringAmount,
                d.RegistrationPeriod, d.RegisteredAt, d.ExpiresAt,
                d.NextDueDate, d.Registrar, d.PaymentMethod,
                d.Status, d.AdminNotes
            })
            .ToListAsync(ct);

        var result = items.Select(d => new DomainReportDto(
            d.Id,
            d.ClientId,
            clientNames.GetValueOrDefault(d.ClientId, ""),
            d.OrderId,
            d.OrderType,
            $"{d.Name}.{d.Tld}",
            d.FirstPaymentAmount,
            d.RecurringAmount,
            d.RegistrationPeriod,
            d.RegisteredAt.ToString("yyyy-MM-dd"),
            d.ExpiresAt.ToString("yyyy-MM-dd"),
            d.NextDueDate.ToString("yyyy-MM-dd"),
            d.Registrar,
            d.PaymentMethod,
            d.Status.ToString(),
            d.AdminNotes)).ToList();

        return new DomainReportResultDto(result, totalCount);
    }

    /// <inheritdoc/>
    public async Task<ClientReportResultDto> GetClientsReportAsync(
        string? status, string? country, DateOnly? createdFrom, DateOnly? createdTo,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Clients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status.ToString() == status);

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(c => c.Country != null && c.Country.ToLower().Contains(country.ToLower()));

        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(c => c.CreatedAt >= from);
        }

        if (createdTo.HasValue)
        {
            var to = createdTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(c => c.CreatedAt <= to);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Users,
                c => c.UserId,
                u => u.Id,
                (c, u) => new ClientReportDto(
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.CompanyName,
                    u.Email ?? "",
                    c.Street,
                    c.Address2,
                    c.City,
                    c.State,
                    c.PostCode,
                    c.Country,
                    c.Phone,
                    c.Currency,
                    c.CreditBalance,
                    c.Status.ToString(),
                    c.CreatedAt.ToString("yyyy-MM-dd"),
                    c.AdminNotes))
            .ToListAsync(ct);

        return new ClientReportResultDto(items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductSuspensionRowDto>> GetProductSuspensionsAsync(CancellationToken ct)
    {
        var raw = await db.ClientServices
            .Where(s => s.Status == Domain.Services.ServiceStatus.Suspended)
            .Join(db.Clients, s => s.ClientId, c => c.Id, (s, c) => new { s, c })
            .Join(db.Products, x => x.s.ProductId, p => p.Id, (x, p) => new
            {
                ServiceId    = x.s.Id,
                ClientName   = x.c.FirstName + " " + x.c.LastName,
                ProductName  = p.Name,
                Domain       = x.s.Domain,
                NextRenewalAt = x.s.NextRenewalAt,
                AdminNotes   = x.s.AdminNotes,
            })
            .OrderBy(r => r.ClientName)
            .ToListAsync(ct);

        return raw.Select(r => new ProductSuspensionRowDto(
            r.ServiceId,
            r.ClientName,
            r.ProductName,
            r.Domain,
            r.NextRenewalAt.HasValue ? r.NextRenewalAt.Value.ToString("yyyy-MM-dd") : null,
            r.AdminNotes)).ToList();
    }

    /// <inheritdoc/>
    public async Task<SupportTicketRepliesDto> GetSupportTicketRepliesAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        int daysInMonth = DateTime.DaysInMonth(year, month);

        // Get all staff replies for this month
        var replies = await db.Tickets
            .SelectMany(t => t.Replies)
            .Where(r => r.IsStaffReply && r.CreatedAt >= from && r.CreatedAt < to)
            .Select(r => new { r.AuthorName, Day = r.CreatedAt.Day })
            .ToListAsync(ct);

        // Group by admin name
        var grouped = replies
            .GroupBy(r => r.AuthorName)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var byDay = g.GroupBy(r => r.Day).ToDictionary(x => x.Key, x => x.Count());
                var dailyCounts = Enumerable.Range(1, daysInMonth)
                    .Select(d => byDay.GetValueOrDefault(d, 0))
                    .ToList();
                return new SupportTicketRepliesRowDto(g.Key, dailyCounts, g.Count());
            })
            .ToList();

        return new SupportTicketRepliesDto(month, year, daysInMonth, grouped);
    }

    /// <inheritdoc/>
    public async Task<SalesTaxReportDto> GetSalesTaxReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var baseQuery = db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid);

        if (from.HasValue)
        {
            var fromDt = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            baseQuery = baseQuery.Where(i => i.CreatedAt >= fromDt);
        }
        if (to.HasValue)
        {
            var toDt = new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
            baseQuery = baseQuery.Where(i => i.CreatedAt <= toDt);
        }

        var rows = await baseQuery
            .OrderBy(i => i.CreatedAt)
            .Join(db.Clients, i => i.ClientId, c => c.Id, (i, c) => new SalesTaxRowDto(
                i.Id,
                c.FirstName + " " + c.LastName,
                i.CreatedAt.ToString("dd/MM/yyyy"),
                i.PaidAt != null ? i.PaidAt.Value.ToString("dd/MM/yyyy") : null,
                i.SubTotal,
                i.Tax,
                i.Credit,
                i.Total))
            .ToListAsync(ct);

        return new SalesTaxReportDto(
            rows.Count,
            rows.Sum(r => r.Total),
            rows.Sum(r => r.Tax),   // Tax Level 1 — total tax
            0m,                      // Tax Level 2 — not used in our system
            rows);
    }

    /// <inheritdoc/>
    public async Task<MonthlyTransactionsReportDto> GetDailyTransactionsAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        // Aggregate transactions by day
        var dailyRows = await db.Transactions
            .Where(t => t.Date >= from && t.Date < to)
            .GroupBy(t => t.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                AmountIn = g.Sum(t => t.AmountIn),
                Fees = g.Sum(t => t.Fees),
                AmountOut = g.Sum(t => t.AmountOut),
            })
            .OrderBy(r => r.Date)
            .ToListAsync(ct);

        // Map aggregated rows by date for quick lookup
        var map = dailyRows.ToDictionary(r => r.Date.Date);

        // Fill all days of the month — zeros for days without transactions
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var rows = new List<DailyTransactionDto>();
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            map.TryGetValue(date, out var r);
            decimal amtIn  = r?.AmountIn  ?? 0m;
            decimal fees   = r?.Fees      ?? 0m;
            decimal amtOut = r?.AmountOut ?? 0m;
            // Balance = daily net (same as WHMCS: AmountIn - Fees - AmountOut)
            decimal balance = amtIn - fees - amtOut;
            rows.Add(new DailyTransactionDto(
                date.ToString("yyyy-MM-dd"),
                amtIn, fees, amtOut, balance));
        }

        return new MonthlyTransactionsReportDto(
            month, year, rows,
            rows.Sum(r => r.AmountIn),
            rows.Sum(r => r.Fees),
            rows.Sum(r => r.AmountOut));
    }
}
