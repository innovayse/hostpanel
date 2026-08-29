namespace Innovayse.Application.Admin.Users.Queries.ListUsers;

/// <summary>Query for one page of the admin user list.</summary>
/// <param name="Search">Optional search term matched against name or email, or null for all users.</param>
/// <param name="Page">1-based page number; values below 1 are treated as 1.</param>
/// <param name="PageSize">Items per page; clamped to 1–100 by the handler.</param>
public record ListUsersQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
