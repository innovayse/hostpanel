using Innovayse.Hostpanel.Application.Common.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>
/// <see cref="TranslationExtensions.ForLocale{T}"/>: the requested locale, then the default, then the
/// first remaining locale in code order, whatever order the translations were stored in.
/// </summary>
public sealed class TranslationExtensionsTests
{
    /// <summary>Picks the requested locale when it is stored.</summary>
    [Fact]
    public void ForLocale_PrefersTheRequestedLocale()
    {
        string[] stored = ["en", "hy", "ru"];

        stored.ForLocale(l => l, "ru", "en").Should().Be("ru");
    }

    /// <summary>Falls back to the default locale when the requested one is missing.</summary>
    [Fact]
    public void ForLocale_FallsBackToTheDefaultLocale()
    {
        string[] stored = ["ru", "en"];

        stored.ForLocale(l => l, "hy", "en").Should().Be("en");
    }

    /// <summary>With neither stored, the answer is the first locale in code order, not in storage order.</summary>
    /// <param name="first">The first locale code.</param>
    /// <param name="second">The second locale code.</param>
    [Theory]
    [InlineData("ru", "hy")]
    [InlineData("hy", "ru")]
    public void ForLocale_WithNeitherStored_IsDeterministic(string first, string second)
    {
        string[] stored = [first, second];

        stored.ForLocale(l => l, "de", "en").Should().Be("hy");
    }

    /// <summary>With nothing stored the answer is null.</summary>
    [Fact]
    public void ForLocale_WithNothingStored_IsNull()
    {
        Array.Empty<string>().ForLocale(l => l, "en", "en").Should().BeNull();
    }
}
