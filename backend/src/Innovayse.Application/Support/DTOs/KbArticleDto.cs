namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a knowledge base article.</summary>
/// <param name="Id">Article primary key.</param>
/// <param name="Title">The article title.</param>
/// <param name="Content">The full article body content.</param>
/// <param name="Category">The category this article belongs to.</param>
/// <param name="IsPublished">Whether the article is visible to clients.</param>
public record KbArticleDto(int Id, string Title, string Content, string Category, bool IsPublished);
