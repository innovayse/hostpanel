using Innovayse.Hostpanel.Domain.Support;

namespace Innovayse.Hostpanel.Domain.Tests.Support;

/// <summary>Unit tests for <see cref="KbSlugRules"/>: the shape, numeric and reserved checks.</summary>
public sealed class KbSlugRulesTests
{
    /// <summary>Well formed slugs are accepted for both articles and categories.</summary>
    /// <param name="slug">The slug under test.</param>
    [Theory]
    [InlineData("getting-started")]
    [InlineData("a")]
    [InlineData("ssl-2")]
    [InlineData("2fa-setup")]
    public void WellFormedSlug_IsValid(string slug)
    {
        Assert.True(KbSlugRules.IsValidArticleSlug(slug));
        Assert.True(KbSlugRules.IsValidCategorySlug(slug));
    }

    /// <summary>Malformed, upper case, numeric and empty slugs are refused.</summary>
    /// <param name="slug">The slug under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Foo")]
    [InlineData("foo bar")]
    [InlineData("foo--bar")]
    [InlineData("-foo")]
    [InlineData("foo-")]
    [InlineData("foo_bar")]
    [InlineData("привет")]
    [InlineData("2024")]
    [InlineData("99999999999")]
    public void MalformedUpperCaseOrNumericSlug_IsRefused(string? slug)
    {
        Assert.False(KbSlugRules.IsValidArticleSlug(slug));
        Assert.False(KbSlugRules.IsValidCategorySlug(slug));
    }

    /// <summary>A slug longer than the column is refused.</summary>
    [Fact]
    public void SlugLongerThanTheLimit_IsRefused()
    {
        Assert.True(KbSlugRules.IsValidArticleSlug(new string('a', KbSlugRules.MaxLength)));
        Assert.False(KbSlugRules.IsValidArticleSlug(new string('a', KbSlugRules.MaxLength + 1)));
    }

    /// <summary>The words the article routes use are refused for articles only.</summary>
    /// <param name="slug">The reserved word.</param>
    [Theory]
    [InlineData("articles")]
    [InlineData("categories")]
    public void ReservedWord_IsRefusedForArticlesButAllowedForCategories(string slug)
    {
        Assert.False(KbSlugRules.IsValidArticleSlug(slug));
        Assert.True(KbSlugRules.IsValidCategorySlug(slug));
    }
}
