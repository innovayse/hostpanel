using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.SharedKernel.Localization;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// There is one list of languages: the shared constants every service reads are built from the kernel's
/// <see cref="LanguageCodes"/>, never from a list of their own.
/// </summary>
public sealed class SupportedLanguagesTests
{
    /// <summary>The shared set holds exactly the kernel's languages and recognises each of them.</summary>
    [Fact]
    public void SupportedLanguages_AreTheKernelLanguages()
    {
        SupportedLanguages.All.Should().BeEquivalentTo(LanguageCodes.All);
        SupportedLanguages.Default.Should().Be(LanguageCodes.Default);
        LanguageCodes.All.Should().OnlyContain(language => SupportedLanguages.IsSupported(language));
        SupportedLanguages.IsSupported("xx").Should().BeFalse();
        SupportedLanguages.IsSupported(null).Should().BeFalse();
    }
}
