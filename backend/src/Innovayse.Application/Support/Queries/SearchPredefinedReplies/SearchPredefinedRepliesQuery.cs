namespace Innovayse.Application.Support.Queries.SearchPredefinedReplies;

/// <summary>Query to search predefined replies by name or content.</summary>
/// <param name="SearchTerm">The search term to match against name and content.</param>
public record SearchPredefinedRepliesQuery(string SearchTerm);
