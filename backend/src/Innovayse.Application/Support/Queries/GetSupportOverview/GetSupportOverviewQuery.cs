namespace Innovayse.Application.Support.Queries.GetSupportOverview;

/// <summary>Query to retrieve support overview dashboard statistics for a given period.</summary>
/// <param name="Period">Time period filter: "today", "yesterday", "last7days", "last30days".</param>
public record GetSupportOverviewQuery(string Period);
