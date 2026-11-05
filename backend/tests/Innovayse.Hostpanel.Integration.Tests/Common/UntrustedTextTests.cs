using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary><see cref="UntrustedText"/>: the one cleaner of text that came from outside.</summary>
public sealed class UntrustedTextTests
{
    /// <summary>Control characters become spaces (a line break cannot forge a second audit line), ends are trimmed, the length is capped.</summary>
    [Fact]
    public void Clean_StripsControlCharactersTrimsAndCaps()
    {
        UntrustedText.Clean("  a\r\nb\tc\u0000d  ", 100).Should().Be("a  b c d");
        UntrustedText.Clean(new string('x', 50), 10).Should().Be(new string('x', 10));
        UntrustedText.Clean("abc   def", 4).Should().Be("abc");
    }

    /// <summary>A missing or blank value is the empty string.</summary>
    /// <param name="value">The input.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void Clean_ABlankValueIsEmpty(string? value)
    {
        UntrustedText.Clean(value, 10).Should().BeEmpty();
    }
}
