namespace Innovayse.Application.Support.Queries.ListPredefinedReplies;

/// <summary>Query to retrieve all predefined replies, optionally filtered by category.</summary>
/// <param name="CategoryId">Optional category FK filter.</param>
public record ListPredefinedRepliesQuery(int? CategoryId = null);
