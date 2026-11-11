namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>A category that has published articles, as the public list shows it.</summary>
/// <param name="Name">The category name.</param>
/// <param name="ArticleCount">How many published articles it holds.</param>
/// <param name="Slug">The category's address segment.</param>
public sealed record PublishedKbCategoryDto(string Name, int ArticleCount, string Slug);
