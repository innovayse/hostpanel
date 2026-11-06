using Innovayse.Hostpanel.Application.Common.Enums;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListUpcomingServiceRenewals;

/// <summary>Lists the active services whose next renewal falls inside a window, soonest first, whether or not they renew automatically.</summary>
/// <param name="Window">How far ahead to look.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (max 100).</param>
public sealed record ListUpcomingServiceRenewalsQuery(RenewalWindow Window = RenewalWindow.Next30Days, int Page = 1, int PageSize = 20);
