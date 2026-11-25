namespace Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;

/// <summary>Query that returns the most recent noteworthy events for the admin notification feed, each marked read or not for the caller.</summary>
/// <param name="Limit">Maximum number of events to return, newest first.</param>
public sealed record GetRecentActivityQuery(int Limit = 10)
{
    /// <summary>The most events this feed will ever return in one call.</summary>
    public const int MaxLimit = 50;
}
