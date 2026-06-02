namespace Innovayse.Application.Support.Queries.ListDownloads;

/// <summary>Query to retrieve downloads, optionally filtered by category.</summary>
/// <param name="CategoryId">Optional category ID to filter by. Pass null to list all downloads.</param>
public record ListDownloadsQuery(int? CategoryId);
