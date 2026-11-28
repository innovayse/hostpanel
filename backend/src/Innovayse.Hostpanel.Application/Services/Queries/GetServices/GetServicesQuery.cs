using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServices;

/// <summary>Returns a paginated list of client services (admin view and a client's services tab), with optional search and filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only services whose own domain, linked registered domain, product name or client (name, company, sign-in email) contain it (case-insensitive),
/// or whose id it is, are returned. Blank returns all.
/// </param>
/// <param name="ClientId">Optional client ID to filter services by owner; null returns all.</param>
/// <param name="Status">Optional lifecycle status, bound by name from the query string; null returns every status.</param>
/// <param name="ProductId">Optional id of the ordered product; null returns every product.</param>
/// <param name="BillingCycle">Optional billing cycle, bound by name from the query string; null returns every cycle.</param>
public sealed record GetServicesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? ClientId = null,
    ServiceStatus? Status = null,
    int? ProductId = null,
    BillingCycle? BillingCycle = null) : IListQuery;
