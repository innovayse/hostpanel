namespace Innovayse.Hostpanel.Application.Support.Queries.SearchPredefinedReplies;

/// <summary>Query to search predefined replies by name or content.</summary>
/// <param name="Search">Required text; only replies whose name or content contains it (case-insensitive) are returned.</param>
public sealed record SearchPredefinedRepliesQuery(string Search);
