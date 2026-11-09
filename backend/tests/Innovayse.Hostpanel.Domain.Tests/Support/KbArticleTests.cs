using Innovayse.Hostpanel.Domain.Support;

namespace Innovayse.Hostpanel.Domain.Tests.Support;

/// <summary>Unit tests for the <see cref="KbArticle"/> aggregate's behaviour.</summary>
public sealed class KbArticleTests
{
    /// <summary>Builds an unpublished article in category 3.</summary>
    /// <returns>A new article with no translations.</returns>
    private static KbArticle NewArticle() => KbArticle.Create(3, "getting-started");

    /// <summary>Create stores the category and slug, starts unpublished and without translations.</summary>
    [Fact]
    public void Create_StoresFields_AndStartsUnpublishedWithoutTranslations()
    {
        var article = NewArticle();

        Assert.Equal(3, article.CategoryId);
        Assert.Equal("getting-started", article.Slug);
        Assert.False(article.IsPublished);
        Assert.Empty(article.Translations);
    }

    /// <summary>A category id that is not positive is refused.</summary>
    /// <param name="categoryId">The category id.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithCategoryIdNotPositive_Throws(int categoryId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KbArticle.Create(categoryId, "getting-started"));
    }

    /// <summary>A slug the rules refuse is rejected, including the reserved route words.</summary>
    /// <param name="slug">The slug.</param>
    [Theory]
    [InlineData("Getting-Started")]
    [InlineData("12")]
    [InlineData("articles")]
    [InlineData("categories")]
    [InlineData("")]
    public void Create_WithInvalidSlug_Throws(string slug)
    {
        Assert.Throws<ArgumentException>(() => KbArticle.Create(3, slug));
    }

    /// <summary>Moving the article changes only its category.</summary>
    [Fact]
    public void MoveToCategory_ChangesOnlyTheCategory()
    {
        var article = NewArticle();

        article.MoveToCategory(9);

        Assert.Equal(9, article.CategoryId);
        Assert.Equal("getting-started", article.Slug);
        Assert.Throws<ArgumentOutOfRangeException>(() => article.MoveToCategory(0));
    }

    /// <summary>SetTranslation adds a locale, then replaces it on the second call.</summary>
    [Fact]
    public void SetTranslation_AddsALocale_ThenReplacesIt()
    {
        var article = NewArticle();

        article.SetTranslation("en", "Title", "Body");
        article.SetTranslation("en", "New title", "New body");

        var only = Assert.Single(article.Translations);
        Assert.Equal("en", only.Locale);
        Assert.Equal("New title", only.Title);
        Assert.Equal("New body", only.Content);
    }

    /// <summary>A blank title or body is refused.</summary>
    /// <param name="title">The title.</param>
    /// <param name="content">The body.</param>
    [Theory]
    [InlineData("", "Body")]
    [InlineData("Title", " ")]
    public void SetTranslation_WithBlankText_Throws(string title, string content)
    {
        var article = NewArticle();

        Assert.Throws<ArgumentException>(() => article.SetTranslation("en", title, content));
    }

    /// <summary>RemoveTranslation drops a locale and ignores one the article does not have.</summary>
    [Fact]
    public void RemoveTranslation_DropsTheLocale_AndIgnoresAMissingOne()
    {
        var article = NewArticle();
        article.SetTranslation("en", "T", "B");
        article.SetTranslation("ru", "Т", "Б");

        article.RemoveTranslation("ru");
        article.RemoveTranslation("hy");

        Assert.Equal("en", Assert.Single(article.Translations).Locale);
    }

    /// <summary>ReplaceTranslations makes the translations exactly the given set.</summary>
    [Fact]
    public void ReplaceTranslations_LeavesExactlyTheGivenLocales()
    {
        var article = NewArticle();
        article.SetTranslation("en", "T", "B");
        article.SetTranslation("ru", "Т", "Б");

        article.ReplaceTranslations(new Dictionary<string, KbArticleTranslationContent>
        {
            ["en"] = new("T2", "B2"),
            ["hy"] = new("Վ", "Ա"),
        });

        Assert.Equal(["en", "hy"], article.Translations.Select(t => t.Locale).Order().ToList());
        Assert.Equal("T2", article.Translations.Single(t => t.Locale == "en").Title);
    }

    /// <summary>A null set of translations is refused.</summary>
    [Fact]
    public void ReplaceTranslations_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewArticle().ReplaceTranslations(null!));
    }

    /// <summary>Publish and Unpublish toggle visibility.</summary>
    [Fact]
    public void PublishThenUnpublish_TogglesVisibility()
    {
        var article = NewArticle();

        article.Publish();
        Assert.True(article.IsPublished);

        article.Unpublish();
        Assert.False(article.IsPublished);
    }
}
