# Admin API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Admin API module — Dashboard stats queries (revenue, active services, overdue invoices, open tickets), report queries with date range filtering (revenue report, client growth report, service usage report), Settings aggregate for system configuration, and admin controllers.

**Architecture:** Dashboard queries aggregate data from multiple repositories; reports use raw SQL or compiled queries for performance; Settings aggregate stores key-value configuration; all endpoints require Admin or Reseller role; date range filtering for all reports.

**Tech Stack:** C# 12, ASP.NET Core 8, EF Core 8 + Npgsql with FromSqlRaw for reports, Wolverine CQRS, xUnit + FluentAssertions.

---

## File Map

```
src/Innovayse.Domain/Settings/
  Setting.cs                                        ← AggregateRoot (key, value, description)
  Interfaces/ISettingRepository.cs

src/Innovayse.Application/Admin/
  DTOs/DashboardStatsDto.cs
  DTOs/RevenueReportItemDto.cs
  DTOs/ClientGrowthReportItemDto.cs
  DTOs/ServiceUsageReportItemDto.cs
  DTOs/SettingDto.cs
  Queries/GetDashboardStats/
    GetDashboardStatsQuery.cs
    GetDashboardStatsHandler.cs
  Queries/GetRevenueReport/
    GetRevenueReportQuery.cs
    GetRevenueReportHandler.cs
  Queries/GetClientGrowthReport/
    GetClientGrowthReportQuery.cs
    GetClientGrowthReportHandler.cs
  Queries/GetServiceUsageReport/
    GetServiceUsageReportQuery.cs
    GetServiceUsageReportHandler.cs
  Queries/GetSettings/
    GetSettingsQuery.cs
    GetSettingsHandler.cs
  Commands/UpdateSetting/
    UpdateSettingCommand.cs
    UpdateSettingHandler.cs

src/Innovayse.Infrastructure/Settings/
  Configurations/SettingConfiguration.cs
  SettingRepository.cs

src/Innovayse.API/Admin/
  DashboardController.cs                            ← GetStats endpoint
  ReportsController.cs                              ← All reports
  SettingsController.cs                             ← CRUD settings
```

---

## Task 1: Domain — Setting Aggregate

- [ ] **Step 1: Create Setting aggregate**

```csharp
namespace Innovayse.Domain.Settings;

public sealed class Setting : AggregateRoot
{
    public string Key         { get; private set; } = string.Empty;
    public string Value       { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Setting() { }

    public static Setting Create(string key, string value, string? description)
    {
        return new Setting
        {
            Key = key,
            Value = value,
            Description = description
        };
    }

    public void UpdateValue(string value)
    {
        Value = value;
    }
}
```

- [ ] **Step 2: Create ISettingRepository**
- [ ] **Step 3: Run dotnet format and commit**

---

## Task 2: Application — DTOs

- [ ] **Create DashboardStatsDto**

```csharp
namespace Innovayse.Application.Admin.DTOs;

public record DashboardStatsDto(
    decimal TotalRevenue,
    decimal MonthlyRevenue,
    int ActiveServices,
    int OverdueInvoices,
    int OpenTickets,
    int TotalClients);
```

- [ ] **Create report DTOs (RevenueReportItemDto, ClientGrowthReportItemDto, ServiceUsageReportItemDto)**
- [ ] **Create SettingDto**

---

## Task 3: Application — Dashboard Query

- [ ] **Step 1: Create GetDashboardStatsQuery and handler**

```csharp
namespace Innovayse.Application.Admin.Queries.GetDashboardStats;

public record GetDashboardStatsQuery;

public sealed class GetDashboardStatsHandler
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IClientServiceRepository _serviceRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IClientRepository _clientRepository;

    public async Task<DashboardStatsDto> HandleAsync(GetDashboardStatsQuery query, CancellationToken ct)
    {
        var allInvoices = await _invoiceRepository.ListAllAsync(ct);
        var totalRevenue = allInvoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Total);
        var monthlyRevenue = allInvoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= DateTimeOffset.UtcNow.AddMonths(-1))
            .Sum(i => i.Total);

        var activeServices = await _serviceRepository.CountByStatusAsync(ServiceStatus.Active, ct);
        var overdueInvoices = await _invoiceRepository.CountByStatusAsync(InvoiceStatus.Overdue, ct);
        var openTickets = await _ticketRepository.CountByStatusAsync(TicketStatus.Open, ct);
        var totalClients = await _clientRepository.CountAsync(ct);

        return new DashboardStatsDto(
            totalRevenue,
            monthlyRevenue,
            activeServices,
            overdueInvoices,
            openTickets,
            totalClients);
    }
}
```

---

## Task 4: Application — Report Queries

Create GetRevenueReport, GetClientGrowthReport, GetServiceUsageReport queries with date range filtering.

Example:

```csharp
public record GetRevenueReportQuery(DateTimeOffset StartDate, DateTimeOffset EndDate);

public sealed class GetRevenueReportHandler
{
    private readonly AppDbContext _context;

    public async Task<IReadOnlyList<RevenueReportItemDto>> HandleAsync(GetRevenueReportQuery query, CancellationToken ct)
    {
        var sql = @"
            SELECT
                DATE(paid_at) AS date,
                SUM(total) AS revenue
            FROM invoices
            WHERE status = 'Paid' AND paid_at BETWEEN {0} AND {1}
            GROUP BY DATE(paid_at)
            ORDER BY date";

        var items = await _context.Database
            .SqlQueryRaw<RevenueReportItemDto>(sql, query.StartDate, query.EndDate)
            .ToListAsync(ct);

        return items;
    }
}
```

---

## Task 5: Application — Settings Commands + Queries

Create GetSettings query and UpdateSetting command.

---

## Task 6: Infrastructure — Persistence

Create SettingConfiguration, SettingRepository, update AppDbContext and DependencyInjection, create migration.

---

## Task 7: API — Controllers

- [ ] **DashboardController with GetStats endpoint**

```csharp
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin,Reseller")]
public sealed class DashboardController : ControllerBase
{
    private readonly IMessageBus _bus;

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStatsAsync(CancellationToken ct)
    {
        var stats = await _bus.InvokeAsync<DashboardStatsDto>(new GetDashboardStatsQuery(), ct);
        return Ok(stats);
    }
}
```

- [ ] **ReportsController with revenue, client-growth, service-usage endpoints**
- [ ] **SettingsController with CRUD**

---

## Task 8: Integration Tests

Create tests for dashboard stats and reports.

---

## Self-Review

- [x] Setting aggregate
- [x] Dashboard stats query
- [x] All report queries with date filtering
- [x] Settings CRUD
- [x] Admin controllers
- [x] Integration tests

---

## Execution Handoff

Plan complete. Choose execution method.
