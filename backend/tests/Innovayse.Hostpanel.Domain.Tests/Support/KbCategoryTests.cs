using Innovayse.Hostpanel.Domain.Support;

namespace Innovayse.Hostpanel.Domain.Tests.Support;

/// <summary>Unit tests for the <see cref="KbCategory"/> entity's behaviour.</summary>
public sealed class KbCategoryTests
{
    /// <summary>Create stores every field including the slug.</summary>
    [Fact]
    public void Create_StoresFields()
    {
        var category = KbCategory.Create("Getting started", "getting-started", "How to begin", isHidden: false, parentCategoryId: 4);

        Assert.Equal("Getting started", category.Name);
        Assert.Equal("getting-started", category.Slug);
        Assert.Equal("How to begin", category.Description);
        Assert.False(category.IsHidden);
        Assert.Equal(4, category.ParentCategoryId);
    }

    /// <summary>A blank name or a slug the rules refuse is rejected.</summary>
    /// <param name="name">The name.</param>
    /// <param name="slug">The slug.</param>
    [Theory]
    [InlineData("", "ok")]
    [InlineData("Name", "Not Ok")]
    [InlineData("Name", "42")]
    [InlineData("Name", "")]
    public void Create_WithBlankNameOrInvalidSlug_Throws(string name, string slug)
    {
        Assert.Throws<ArgumentException>(() => KbCategory.Create(name, slug, string.Empty, isHidden: false));
    }

    /// <summary>Renaming a category leaves its slug alone, so links to it keep working.</summary>
    [Fact]
    public void Update_Renames_ButKeepsTheSlug()
    {
        var category = KbCategory.Create("Billing", "billing", string.Empty, isHidden: false);

        category.Update("Payments", "All about payments", isHidden: true);

        Assert.Equal("Payments", category.Name);
        Assert.Equal("billing", category.Slug);
        Assert.Equal("All about payments", category.Description);
        Assert.True(category.IsHidden);
    }
}
