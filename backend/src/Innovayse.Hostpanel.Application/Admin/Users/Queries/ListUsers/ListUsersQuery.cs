using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Users.Queries.ListUsers;

/// <summary>Query for one page of the admin user list.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text matched by the identity provider against name or email, or null for all users.</param>
public sealed record ListUsersQuery(
    int Page,
    int PageSize,
    string? Search = null) : IListQuery;
