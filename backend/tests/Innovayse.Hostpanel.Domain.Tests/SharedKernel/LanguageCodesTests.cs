using Innovayse.Hostpanel.SharedKernel.Localization;

namespace Innovayse.Hostpanel.Domain.Tests.SharedKernel;

/// <summary>Tests for <see cref="LanguageCodes"/>: the one list of languages every per-language rule is built from.</summary>
public sealed class LanguageCodesTests
{
    /// <summary>The default language leads the list and appears once, so a fallback is always the first row.</summary>
    [Fact]
    public void All_StartsWithTheDefaultLanguage_AndHasNoDuplicates()
    {
        LanguageCodes.All[0].Should().Be(LanguageCodes.Default);
        LanguageCodes.All.Should().OnlyHaveUniqueItems();
    }

    /// <summary>The languages with a row of their own are every language but the default, in list order.</summary>
    [Fact]
    public void NonDefault_IsEveryLanguageButTheDefault()
    {
        LanguageCodes.NonDefault.Should().Equal(LanguageCodes.All.Skip(1));
        LanguageCodes.NonDefault.Should().NotContain(LanguageCodes.Default);
    }

    /// <summary>The platform answers in German and Spanish as well as the original three, which a per-language row must cover.</summary>
    [Theory]
    [InlineData("ru")]
    [InlineData("hy")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    public void All_ContainsEveryShippedLanguage(string language)
    {
        LanguageCodes.All.Should().Contain(language);
    }
}
