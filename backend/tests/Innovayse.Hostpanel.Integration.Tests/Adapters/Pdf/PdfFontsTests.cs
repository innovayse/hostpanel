using Innovayse.Hostpanel.Infrastructure.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Pdf;

/// <summary>
/// Tests for <see cref="PdfFonts.ReplaceUncovered"/>: characters no bundled font can print become a
/// visible placeholder instead of a blank, and everything else is left as written.
/// </summary>
/// <remarks>Invisible characters are written as escapes so the reader can see what is under test.</remarks>
public sealed class PdfFontsTests
{
    /// <summary>
    /// Arabic, Hebrew, Chinese, Japanese and Korean come back as one placeholder per character, with
    /// the Latin text, spaces and punctuation around them intact.
    /// </summary>
    /// <param name="text">A name with letters no bundled font has.</param>
    /// <param name="expected">The name as it should print.</param>
    /// <param name="bold">Whether it is set in bold.</param>
    [Theory]
    [InlineData("Ahmed أحمد Ali", "Ahmed ���� Ali", false)]
    [InlineData("Ahmed أحمد Ali", "Ahmed ���� Ali", true)]
    [InlineData("דוד Cohen", "��� Cohen", false)]
    [InlineData("王小明 (Wang)", "��� (Wang)", false)]
    [InlineData("山田 たろう", "�� ���", false)]
    [InlineData("김민준, Seoul", "���, Seoul", true)]
    public void ReplaceUncovered_PrintsOnePlaceholderPerUncoveredCharacter(string text, string expected, bool bold)
    {
        PdfFonts.ReplaceUncovered(text, bold).Should().Be(expected);
    }

    /// <summary>
    /// Text the bundled fonts cover comes back exactly as written, in either weight, as the same
    /// instance: nothing is copied for the common case.
    /// </summary>
    /// <param name="text">Text in a script the fonts cover.</param>
    [Theory]
    [InlineData("Fairhaven Hosting — Starter, $12.50")]
    [InlineData("Арам Петросян")]
    [InlineData("Ծաղկաձոր, Հայաստան")]
    [InlineData("გიორგი ბერიძე")]
    [InlineData("Αλέξης Παπαδόπουλος")]
    [InlineData("Nguyễn Văn An")]
    [InlineData("Nguye\u0302\u0303n")]
    [InlineData("6,000 \u058F")]
    [InlineData("\u041C\u0430\u0440\u02BC\u044F\u043D\u0430 \u041A\u043E\u0432\u0430\u043B\u044C")]
    [InlineData("")]
    public void ReplaceUncovered_LeavesCoveredTextAsWritten(string text)
    {
        PdfFonts.ReplaceUncovered(text, bold: false).Should().BeSameAs(text);
        PdfFonts.ReplaceUncovered(text, bold: true).Should().BeSameAs(text);
    }

    /// <summary>
    /// A character built from several code points is one placeholder, not one per code point, and a
    /// covered letter carrying a mark no font has is replaced whole. An invisible joiner inside such
    /// a character goes with it.
    /// </summary>
    /// <param name="text">A single user-perceived character, or text containing one.</param>
    /// <param name="expected">The text as it should print.</param>
    [Theory]
    [InlineData("\u0628\u0650", "�")]
    [InlineData("\u05D3\u05BC", "�")]
    [InlineData("a\u0610", "�")]
    [InlineData("\u06CC\u200C", "�")]
    [InlineData("Team \U0001F468\u200D\U0001F469\u200D\U0001F467", "Team �")]
    [InlineData("Thanks \U0001F44D\U0001F3FD", "Thanks �")]
    public void ReplaceUncovered_PrintsOnePlaceholderPerCharacterNotPerCodePoint(string text, string expected)
    {
        PdfFonts.ReplaceUncovered(text, bold: false).Should().Be(expected);
    }

    /// <summary>
    /// Invisible characters a font maps, and the tabs and line breaks MigraDoc lays out itself, are
    /// kept: the renderer adds zero-width spaces to break long words, and a placeholder there would
    /// print a mark the client never typed.
    /// </summary>
    /// <param name="text">Text with an invisible character in it.</param>
    [Theory]
    [InlineData("a\u200Bb")]
    [InlineData("a\u200Db")]
    [InlineData("a\u200Eb\u200F")]
    [InlineData("a\u2028b\u2029c")]
    [InlineData("a\u00ADb")]
    [InlineData("6\u00A0000")]
    [InlineData("a\tb")]
    [InlineData("Line one\nLine two")]
    [InlineData("Line one\r\nLine two")]
    public void ReplaceUncovered_KeepsInvisibleCharactersThatPrintAsNothing(string text)
    {
        PdfFonts.ReplaceUncovered(text, bold: false).Should().Be(text);
    }

    /// <summary>
    /// An invisible character no font maps is dropped rather than replaced: left in, it would print
    /// as the font's missing-glyph box, and a placeholder would print a mark the client never typed.
    /// </summary>
    /// <param name="text">Text with an invisible character no bundled font has.</param>
    /// <param name="expected">The text as it should print.</param>
    [Theory]
    [InlineData("A\u061CB", "AB")]
    [InlineData("\uFEFFAli", "Ali")]
    [InlineData("a\u2066b\u2069", "ab")]
    [InlineData("a\u0007b", "ab")]
    [InlineData("\u0623\u061C\u062D", "��")]
    public void ReplaceUncovered_DropsInvisibleCharactersNoFontHas(string text, string expected)
    {
        PdfFonts.ReplaceUncovered(text, bold: false).Should().Be(expected);
    }

    /// <summary>A space no font has, such as the ideographic space, becomes a plain space.</summary>
    [Fact]
    public void ReplaceUncovered_TurnsSpacesNoFontHasIntoPlainSpaces()
    {
        PdfFonts.ReplaceUncovered("\u5C71\u7530\u3000\u592A\u90CE", bold: false).Should().Be("�� ��");
    }

    /// <summary>A zero-width space between uncovered letters stays, so the word can still break there.</summary>
    [Fact]
    public void ReplaceUncovered_KeepsBreaksBetweenUncoveredLetters()
    {
        PdfFonts.ReplaceUncovered("\u738B\u200B\u5C0F", bold: false).Should().Be("�\u200B�");
    }

    /// <summary>
    /// A lone surrogate (half of a pair, from text cut in the wrong place) is not a character at
    /// all, so it prints as a placeholder rather than reaching the PDF as broken text.
    /// </summary>
    [Fact]
    public void ReplaceUncovered_PrintsAPlaceholderForALoneSurrogate()
    {
        PdfFonts.ReplaceUncovered("a" + (char)0xD83D + "b", bold: false).Should().Be("a" + PdfFonts.Placeholder + "b");
    }

    /// <summary>
    /// No bundled face has a glyph outside the Basic Multilingual Plane, which the coverage tables
    /// rely on. A face that gains one (an emoji font, say) fails here rather than printing it blank.
    /// </summary>
    /// <param name="family">The family to check.</param>
    /// <param name="bold">Which weight to check.</param>
    [Theory]
    [InlineData(PdfFonts.Primary, false)]
    [InlineData(PdfFonts.Primary, true)]
    [InlineData(PdfFonts.Fallback, false)]
    [InlineData(PdfFonts.Fallback, true)]
    public void EveryFace_HasNoGlyphOutsideTheBasicMultilingualPlane(string family, bool bold)
    {
        PdfFonts.EnsureInitialized();
        var font = new XFont(family, 10, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);

        Enumerable.Range(0x10000, 0x110000 - 0x10000)
            .Where(codePoint => GlyphHelper.GlyphIndexFromCodePoint(codePoint, font) != 0)
            .Should().BeEmpty();
    }

    /// <summary>
    /// Runs switch to the fallback family only for the characters the main one lacks, and together
    /// spell the text again.
    /// </summary>
    [Fact]
    public void Runs_SetsOnlyWhatTheMainFamilyLacksInTheFallback()
    {
        PdfFonts.Runs("6,000 \u058F due", bold: false).Should().Equal(
            ("6,000 ", false), ("\u058F", true), (" due", false));
    }

    /// <summary>Text the main family covers throughout is one run, the text itself.</summary>
    [Fact]
    public void Runs_KeepsCoveredTextInOneRun()
    {
        const string text = "Shared hosting — Starter";

        PdfFonts.Runs(text, bold: true).Should().Equal((text, false));
    }

    /// <summary>
    /// The placeholder itself has a glyph in the main family, in both weights, so a later change of
    /// font cannot quietly turn it into the very blank it stands in for.
    /// </summary>
    /// <param name="bold">Which weight to check.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Placeholder_HasAGlyphInTheMainFamily(bool bold)
    {
        PdfFonts.EnsureInitialized();
        var font = new XFont(PdfFonts.Primary, 10, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);

        GlyphHelper.GlyphIndexFromCodePoint(PdfFonts.Placeholder, font).Should().NotBe(0);
    }
}
