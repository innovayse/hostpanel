namespace Innovayse.Application.Admin.Queries.GetRecentActivity;

/// <summary>Query that returns the most recent noteworthy events for the admin notification feed.</summary>
/// <param name="Limit">Maximum number of events to return, newest first.</param>
public record GetRecentActivityQuery(int Limit = 10);
