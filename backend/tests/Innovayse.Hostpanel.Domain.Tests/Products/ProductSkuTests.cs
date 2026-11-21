using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Unit tests for the <see cref="ProductSku"/> format rule.</summary>
public class ProductSkuTests
{
    /// <summary>Normalize trims surrounding white space and upper-cases the SKU.</summary>
    /// <param name="input">The SKU as typed.</param>
    /// <param name="expected">The stored form.</param>
    [Theory]
    [InlineData("lic-pro", "LIC-PRO")]
    [InlineData("  Credits_100 ", "CREDITS_100")]
    [InlineData("PLAN.V2", "PLAN.V2")]
    [InlineData(" Plan:Pro ", "PLAN:PRO")]
    public void Normalize_TrimsAndUpperCases(string input, string expected)
    {
        Assert.Equal(expected, ProductSku.Normalize(input));
    }

    /// <summary>Normalize turns null, empty and white-space text into no SKU.</summary>
    /// <param name="input">The empty input.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Normalize_WithNothing_ReturnsNull(string? input)
    {
        Assert.Null(ProductSku.Normalize(input));
    }

    /// <summary>IsValid accepts alphanumeric words joined by single separators, alone or as one family:value pair, in any case.</summary>
    /// <param name="sku">A well-formed SKU.</param>
    [Theory]
    [InlineData("A")]
    [InlineData("123")]
    [InlineData("LIC-PRO-1Y")]
    [InlineData("credits_100")]
    [InlineData(" plan.v2 ")]
    [InlineData("A-B_C.D")]
    [InlineData("plan:pro")]
    [InlineData("credits:100")]
    [InlineData(" Licence-Pro.v2:1Y_Eu ")]
    public void IsValid_AcceptsAWellFormedSku(string sku)
    {
        Assert.True(ProductSku.IsValid(sku));
    }

    /// <summary>IsValid refuses empty text, stray or doubled separators, a second or edge colon and other characters.</summary>
    /// <param name="sku">A malformed SKU.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("LIC PRO")]
    [InlineData("LIC--PRO")]
    [InlineData("LIC-.PRO")]
    [InlineData("-LIC")]
    [InlineData("LIC_")]
    [InlineData(".LIC")]
    [InlineData("LIC/PRO")]
    [InlineData("LIC#1")]
    [InlineData("ЛИЦ")]
    [InlineData(":")]
    [InlineData("PLAN:")]
    [InlineData(":PRO")]
    [InlineData("PLAN::PRO")]
    [InlineData("A:B:C")]
    [InlineData("PLAN:-PRO")]
    [InlineData("PLAN-:PRO")]
    [InlineData("PLAN: PRO")]
    public void IsValid_RefusesAMalformedSku(string? sku)
    {
        Assert.False(ProductSku.IsValid(sku));
    }

    /// <summary>IsValid accepts a SKU of exactly the maximum length and refuses one character more.</summary>
    [Fact]
    public void IsValid_EnforcesTheMaximumLength()
    {
        Assert.True(ProductSku.IsValid(new string('A', ProductSku.MaxLength)));
        Assert.False(ProductSku.IsValid(new string('A', ProductSku.MaxLength + 1)));
    }
}
