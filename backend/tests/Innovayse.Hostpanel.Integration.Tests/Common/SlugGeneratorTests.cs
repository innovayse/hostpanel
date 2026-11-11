using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>Unit tests for <see cref="SlugGenerator"/>: what a text becomes, and the cases that leave nothing usable.</summary>
public sealed class SlugGeneratorTests
{
    /// <summary>The longest slug these cases may build.</summary>
    private const int Max = 40;

    /// <summary>A text becomes lower case Latin letters and digits joined by single hyphens, accents dropped.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The slug expected.</param>
    [Theory]
    [InlineData("Getting Started", "getting-started")]
    [InlineData("  Hello,   World!  ", "hello-world")]
    [InlineData("--Already--hyphenated--", "already-hyphenated")]
    [InlineData("Café Crème Brûlée", "cafe-creme-brulee")]
    [InlineData("Doğru", "dogru")]
    [InlineData("Ångström Æble Œuvre", "angstrom-aeble-oeuvre")]
    [InlineData("Straße", "strasse")]
    [InlineData("Łódź Ørsted Đurđevak", "lodz-orsted-durdevak")]
    [InlineData("2FA setup", "2fa-setup")]
    [InlineData("1 2 3", "1-2-3")]
    [InlineData("Привет World", "world")]
    [InlineData("a_b/c\\d", "a-b-c-d")]
    public void Text_BecomesAClean_LowerCaseSlug(string text, string expected)
    {
        SlugGenerator.FromText(text, "article", Max).Should().Be(expected);
    }

    /// <summary>A text with nothing the slug can keep gives the fallback.</summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ???")]
    [InlineData("Привет")]
    [InlineData("Բարեւ")]
    [InlineData("日本語")]
    public void TextWithNothingUsable_GivesTheFallback(string? text)
    {
        SlugGenerator.FromText(text, "article", Max).Should().Be("article");
    }

    /// <summary>A purely numeric result is prefixed with the fallback, so it can never be read as an id.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The slug expected.</param>
    [Theory]
    [InlineData("2024", "article-2024")]
    [InlineData("  007  ", "article-007")]
    public void NumericResult_GetsThePrefix(string text, string expected)
    {
        SlugGenerator.FromText(text, "article", Max).Should().Be(expected);
    }

    /// <summary>A long text is cut to the limit without a trailing hyphen, and a numeric one stays within it.</summary>
    [Fact]
    public void LongText_IsCutWithoutATrailingHyphen_AndStaysWithinTheLimit()
    {
        var cutAtAHyphen = SlugGenerator.FromText(new string('a', Max - 1) + " " + new string('b', 20), "article", Max);
        var numeric = SlugGenerator.FromText(new string('7', 100), "article", Max);

        cutAtAHyphen.Should().Be(new string('a', Max - 1));
        numeric.Length.Should().BeLessThanOrEqualTo(Max);
        numeric.Should().StartWith("article-7");
    }

    /// <summary>A limit too small to hold the fallback and a suffix is refused.</summary>
    [Fact]
    public void LimitTooSmall_IsRefused()
    {
        var act = () => SlugGenerator.FromText("Anything", "article", 5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>A blank fallback is refused.</summary>
    [Fact]
    public void BlankFallback_IsRefused()
    {
        var act = () => SlugGenerator.FromText("Anything", " ", Max);

        act.Should().Throw<ArgumentException>();
    }
}
