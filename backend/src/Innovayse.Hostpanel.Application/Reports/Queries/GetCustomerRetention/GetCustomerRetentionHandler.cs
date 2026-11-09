using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Common.Enums;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetCustomerRetention;

/// <summary>Handles <see cref="GetCustomerRetentionQuery"/>.</summary>
/// <remarks>
/// The count and the average lifetime per product and billing cycle are aggregated in SQL, one
/// query for services and one for domains; only the handful of aggregate rows comes back, and they
/// are merged into product groups here. A lifetime runs from creation to termination (or, for a
/// service still running, to its next renewal); a domain's from registration to expiry. One with
/// no end yet is measured up to now.
/// </remarks>
public sealed class GetCustomerRetentionHandler
{
    /// <summary>The registration term of a domain, which is registered a year at a time.</summary>
    private const int DomainTermYears = 1;

    /// <summary>Days in a year of a retention span.</summary>
    private const int DaysPerYear = 365;

    /// <summary>Days in a month of a retention span.</summary>
    private const int DaysPerMonth = 30;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment.</param>
    public GetCustomerRetentionHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">Whether services and domains still running count towards the average.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Average retention per product, grouped by product group. Never refuses.</returns>
    public async Task<Result<CustomerRetentionDto>> HandleAsync(GetCustomerRetentionQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;

        var services = _context.ClientServices.AsNoTracking().AsQueryable();
        if (!query.IncludeActive)
        {
            services = services.Where(s => s.Status == ServiceStatus.Terminated);
        }

        var serviceRows = await services
            .Join(_context.Products.AsNoTracking(), s => s.ProductId, p => p.Id, (s, p) => new { s, p })
            .Join(_context.ProductGroups.AsNoTracking(), x => x.p.GroupId, g => g.Id, (x, g) => new
            {
                GroupName = g.Name,
                ProductName = x.p.Name,
                x.s.BillingCycle,
                x.s.CreatedAt,
                EndAt = (x.s.Status == ServiceStatus.Terminated ? x.s.TerminatedAt : x.s.NextRenewalAt) ?? now,
            })
            .GroupBy(x => new { x.GroupName, x.ProductName, x.BillingCycle })
            .Select(g => new
            {
                g.Key.GroupName,
                g.Key.ProductName,
                g.Key.BillingCycle,
                Count = g.Count(),
                AvgDays = g.Average(x => (x.EndAt - x.CreatedAt).TotalDays),
            })
            .ToListAsync(ct);

        var domains = _context.Domains.AsNoTracking().AsQueryable();
        if (!query.IncludeActive)
        {
            domains = domains.Where(d => d.Status == DomainStatus.Expired || d.Status == DomainStatus.Cancelled);
        }

        var domainRows = await domains
            .GroupBy(d => d.Tld)
            .Select(g => new
            {
                ProductName = g.Key,
                Count = g.Count(),
                AvgDays = g.Average(d => (d.ExpiresAt - d.RegisteredAt).TotalDays),
            })
            .ToListAsync(ct);

        var groups = serviceRows
            .GroupBy(r => r.GroupName)
            .OrderBy(grp => grp.Key, StringComparer.Ordinal)
            .Select(grp => new CustomerRetentionGroupDto(
                RetentionGroupKind.ProductGroup,
                grp.Key,
                grp
                    .Select(r => ToRow(r.ProductName, r.BillingCycle, null, r.Count, r.AvgDays))
                    .OrderBy(r => r.ProductName, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        if (domainRows.Count > 0)
        {
            groups.Add(new CustomerRetentionGroupDto(
                RetentionGroupKind.Domains,
                null,
                domainRows
                    .Select(r => ToRow(r.ProductName, null, DomainTermYears, r.Count, r.AvgDays))
                    .OrderBy(r => r.ProductName, StringComparer.Ordinal)
                    .ToList()));
        }

        return Result<CustomerRetentionDto>.Success(new CustomerRetentionDto(groups));
    }

    /// <summary>Builds a report row, splitting the average lifetime into whole years and months.</summary>
    /// <param name="productName">The product or top-level domain.</param>
    /// <param name="billingCycle">The billing cycle of a service; null for a domain.</param>
    /// <param name="termYears">The registration term of a domain; null for a service.</param>
    /// <param name="count">How many services or domains the average covers.</param>
    /// <param name="avgDays">The average lifetime in days.</param>
    /// <returns>The row.</returns>
    private static CustomerRetentionRowDto ToRow(string productName, BillingCycle? billingCycle, int? termYears, int count, double avgDays)
    {
        var days = (int)avgDays;
        return new CustomerRetentionRowDto(
            productName,
            billingCycle,
            termYears,
            count,
            days,
            days / DaysPerYear,
            (days % DaysPerYear) / DaysPerMonth);
    }
}
