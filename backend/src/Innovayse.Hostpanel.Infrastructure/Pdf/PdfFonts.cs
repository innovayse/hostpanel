using System.Buffers;
using System.Globalization;
using System.Text;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>
/// The fonts invoice PDFs are set in, served to PDFsharp from the files embedded in this assembly.
/// </summary>
/// <remarks>
/// <para>
/// The production image is Alpine with no fonts installed, so PDFsharp must never look for system
/// fonts: every family it asks for resolves to an embedded file here, and nothing else.
/// </para>
/// <para>
/// DejaVu Sans covers Latin, Greek, Cyrillic, Armenian and Georgian letters in one face. It lacks
/// the dram sign (U+058F), the suffix of the Armenian dram, so Noto Sans Armenian stands behind it
/// for any character DejaVu has no glyph for. Both licences sit next to the files in
/// <c>Pdf/Fonts</c>.
/// </para>
/// <para>
/// A character neither family has (Arabic, Hebrew, CJK, emoji) would print as a blank, so
/// <see cref="ReplaceUncovered"/> prints <see cref="Placeholder"/> in its place.
/// </para>
/// <para>
/// Which characters each face has is read once per process into immutable tables, so asking costs
/// a bit test and no <see cref="XFont"/> is shared between the renders running side by side.
/// </para>
/// <para>
/// PDFsharp's font resolver and licence are process-wide, so both are set once, by
/// <see cref="EnsureInitialized"/>, and nothing else in the process may set them.
/// </para>
/// </remarks>
internal static class PdfFonts
{
    /// <summary>The family every invoice is set in.</summary>
    public const string Primary = "DejaVu Sans";

    /// <summary>The family for characters <see cref="Primary"/> has no glyph for.</summary>
    public const string Fallback = "Noto Sans Armenian";

    /// <summary>
    /// Printed instead of a character neither family has a glyph for: U+FFFD, the Unicode
    /// replacement character, which <see cref="Primary"/> has in both weights.
    /// </summary>
    public const char Placeholder = '\uFFFD';

    /// <summary>The size the faces are opened at to ask which glyphs they have; any size answers alike.</summary>
    private const double ProbeSize = 10;

    /// <summary>
    /// The regular faces' coverage, read the first time it is needed. A read that throws is not
    /// cached, so the next render tries again; two renders racing to read it both build the same table.
    /// </summary>
    private static readonly Lazy<Faces> RegularFaces = new(() => ReadFaces(XFontStyleEx.Regular), LazyThreadSafetyMode.PublicationOnly);

    /// <summary>The bold faces' coverage, read the way <see cref="RegularFaces"/> is.</summary>
    private static readonly Lazy<Faces> BoldFaces = new(() => ReadFaces(XFontStyleEx.Bold), LazyThreadSafetyMode.PublicationOnly);

    /// <summary>Sets PDFsharp up the first time it is read; the value itself means nothing.</summary>
    private static readonly Lazy<bool> Initialized = new(() =>
    {
        // 6.5 onwards refuses to render until the AGPL licence is accepted in code.
        PdfSharpLicense.SetAgplLicense();
        GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
        return true;
    });

    /// <summary>Accepts PDFsharp's licence and installs the embedded fonts, once per process.</summary>
    public static void EnsureInitialized() => _ = Initialized.Value;

    /// <summary>
    /// Replaces every character neither family can print with <see cref="Placeholder"/>, so it
    /// shows as a mark instead of a blank.
    /// </summary>
    /// <param name="text">The text to set.</param>
    /// <param name="bold">Whether it is set in bold.</param>
    /// <returns>The text with each uncovered character replaced, and everything else as written apart from the invisible characters described below.</returns>
    /// <remarks>
    /// <para>
    /// A character is what a reader sees as one (a grapheme cluster), so a letter with its marks or
    /// an emoji sequence becomes a single placeholder. It is replaced whole when any of its code
    /// points has no glyph in either family.
    /// </para>
    /// <para>
    /// An invisible character on its own (a control, a format character such as the zero-width
    /// space, or a separator) is never replaced. It is kept when a family maps it, or when it is a
    /// tab or line break, which MigraDoc lays out itself. Otherwise it would print as the font's
    /// missing-glyph box, so a space becomes a plain space and anything else is dropped. An
    /// invisible joiner inside an uncovered character goes with it.
    /// </para>
    /// <para>
    /// No font has the variation selectors (U+FE0E, U+FE0F and the rest), which only ask for a text
    /// or an emoji look. A character that prints once its selectors are dropped, such as a trademark
    /// sign typed on a phone, prints without them rather than as a placeholder, and a selector with no
    /// character before it is dropped.
    /// </para>
    /// <para>
    /// A space carrying a mark no font has, and a number sign joined to the digit after it (the
    /// Arabic number sign, say), are split: the space or the digit prints, and only the rest
    /// becomes a placeholder. A line separator MigraDoc does not break at (U+2028, U+2029, a
    /// vertical tab, a form feed, U+0085) becomes a line feed.
    /// </para>
    /// <para>
    /// Text every character of which prints as written comes back as the same instance, with
    /// nothing allocated. A lone surrogate is not a character, so it is replaced too.
    /// </para>
    /// </remarks>
    public static string ReplaceUncovered(string text, bool bold)
    {
        var faces = FacesFor(bold);
        if (PrintsAsWritten(text, faces))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var rest = text.AsSpan();
        while (!rest.IsEmpty)
        {
            var character = rest[..StringInfo.GetNextTextElementLength(rest)];
            rest = rest[character.Length..];
            AppendPrintable(result, character, faces);
        }

        return result.ToString();
    }

    /// <summary>
    /// Splits text into runs, each set in <see cref="Primary"/> or, for characters only the
    /// fallback has, in <see cref="Fallback"/>.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="bold">Whether it is set in bold.</param>
    /// <returns>The runs in order; together they spell <paramref name="text"/>.</returns>
    public static IEnumerable<(string Text, bool UseFallback)> Runs(string text, bool bold)
    {
        var faces = FacesFor(bold);
        var start = 0;
        var end = 0;
        var runIsFallback = false;
        foreach (var rune in text.EnumerateRunes())
        {
            var useFallback = !faces.Primary.Has(rune) && faces.Fallback.Has(rune);
            if (useFallback != runIsFallback && end > start)
            {
                yield return (text[start..end], runIsFallback);
                start = end;
            }

            runIsFallback = useFallback;
            end += rune.Utf16SequenceLength;
        }

        if (end > start)
        {
            yield return (text[start..end], runIsFallback);
        }
    }

    /// <summary>Appends what one character prints as: itself, part of it, a line feed, a placeholder or nothing.</summary>
    /// <param name="result">Where the printable text goes.</param>
    /// <param name="character">One character, as a grapheme cluster, or the part of one left to print.</param>
    /// <param name="faces">The faces it is set in.</param>
    private static void AppendPrintable(StringBuilder result, ReadOnlySpan<char> character, Faces faces)
    {
        if (PrintsAsWritten(character, faces))
        {
            result.Append(character);
            return;
        }

        Rune.DecodeFromUtf16(character, out var first, out var firstLength);
        if (IsLineSeparator(first))
        {
            // A line separator is a character of its own, never joined to what follows it.
            result.Append('\n');
            return;
        }

        var bare = WithoutVariationSelectors(character);
        if (bare.Length == 0 || PrintsAsWritten(bare, faces))
        {
            result.Append(bare);
            return;
        }

        // A space joins a mark after it into one character, and a number sign joins the digit after
        // it; replacing either whole would lose the space or the digit, so each part prints apart.
        if (firstLength < character.Length
            && (Rune.GetUnicodeCategory(first) is UnicodeCategory.SpaceSeparator || IsNumberSign(first)))
        {
            AppendPrintable(result, character[..firstLength], faces);
            AppendPrintable(result, character[firstLength..], faces);
            return;
        }

        var invisible = true;
        var space = false;
        foreach (var rune in character.EnumerateRunes())
        {
            invisible &= IsInvisible(rune);
            space |= Rune.GetUnicodeCategory(rune) is UnicodeCategory.SpaceSeparator;
        }

        if (!invisible)
        {
            result.Append(Placeholder);
        }
        else if (space)
        {
            result.Append(' ');
        }
    }

    /// <summary>The coverage of the faces text is set in, in one weight.</summary>
    /// <param name="bold">Whether the text is bold.</param>
    /// <returns>The coverage of both families in that weight.</returns>
    private static Faces FacesFor(bool bold) => (bold ? BoldFaces : RegularFaces).Value;

    /// <summary>Reads which characters both families have in one weight.</summary>
    /// <param name="style">The weight.</param>
    /// <returns>Their coverage.</returns>
    private static Faces ReadFaces(XFontStyleEx style)
    {
        EnsureInitialized();
        return new Faces(new FaceCoverage(Primary, style), new FaceCoverage(Fallback, style));
    }

    /// <summary>
    /// Whether every character of some text prints as written: each has a glyph in one of the
    /// families or is a tab or line break, and none is a lone surrogate or a line separator MigraDoc
    /// would not break at.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="faces">The faces it is set in.</param>
    /// <returns>True when nothing in it needs replacing.</returns>
    private static bool PrintsAsWritten(ReadOnlySpan<char> text, Faces faces)
    {
        while (!text.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(text, out var rune, out var length) != OperationStatus.Done
                || IsLineSeparator(rune)
                || !(IsLaidOut(rune) || faces.Primary.Has(rune) || faces.Fallback.Has(rune)))
            {
                return false;
            }

            text = text[length..];
        }

        return true;
    }

    /// <summary>The text with every variation selector taken out.</summary>
    /// <param name="text">One character, as a grapheme cluster.</param>
    /// <returns>The text without its variation selectors; empty when it had nothing else.</returns>
    private static string WithoutVariationSelectors(ReadOnlySpan<char> text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (!IsVariationSelector(rune))
            {
                result.Append(rune.ToString());
            }
        }

        return result.ToString();
    }

    /// <summary>Whether a character is a variation selector: U+FE00 to U+FE0F, or U+E0100 to U+E01EF.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>True when it only selects a look for the character before it.</returns>
    private static bool IsVariationSelector(Rune rune) => rune.Value is (>= 0xFE00 and <= 0xFE0F) or (>= 0xE0100 and <= 0xE01EF);

    /// <summary>Whether MigraDoc lays a character out itself instead of drawing a glyph: a tab or a line break.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>True when it needs no glyph.</returns>
    private static bool IsLaidOut(Rune rune) => rune.Value is '\t' or '\n' or '\r';

    /// <summary>
    /// Whether a character ends a line but is not one MigraDoc breaks at: the Unicode line and
    /// paragraph separators, a vertical tab (PowerPoint's soft line break), a form feed or a
    /// next-line control.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns>True when it should print as a line feed.</returns>
    private static bool IsLineSeparator(Rune rune) => rune.Value is '\v' or '\f' or '\u0085' or '\u2028' or '\u2029';

    /// <summary>
    /// Whether a character is a sign written before the digits it spans, such as the Arabic number
    /// sign: a format character by category, but a visible mark, and one that joins the digit after
    /// it into a single character.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns>True for U+0600 to U+0605, U+06DD, U+070F, U+0890, U+0891, U+08E2, U+110BD and U+110CD.</returns>
    private static bool IsNumberSign(Rune rune) => rune.Value is (>= 0x0600 and <= 0x0605)
        or 0x06DD or 0x070F or 0x0890 or 0x0891 or 0x08E2 or 0x110BD or 0x110CD;

    /// <summary>
    /// Whether a character prints nothing visible: a control, a format character other than a
    /// number sign, or a separator.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns>True when no placeholder should ever stand in for it.</returns>
    private static bool IsInvisible(Rune rune) => !IsNumberSign(rune) && Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
        or UnicodeCategory.Format
        or UnicodeCategory.SpaceSeparator
        or UnicodeCategory.LineSeparator
        or UnicodeCategory.ParagraphSeparator;

    /// <summary>The coverage of the two families in one weight.</summary>
    /// <param name="Primary">The coverage of <see cref="PdfFonts.Primary"/>.</param>
    /// <param name="Fallback">The coverage of <see cref="PdfFonts.Fallback"/>.</param>
    private sealed record Faces(FaceCoverage Primary, FaceCoverage Fallback);

    /// <summary>
    /// Which characters of the Basic Multilingual Plane one face has a glyph for, one bit each,
    /// read once and never changed. The bundled faces have nothing beyond that plane.
    /// </summary>
    private sealed class FaceCoverage
    {
        /// <summary>One bit per code point from U+0000 to U+FFFF.</summary>
        private readonly ulong[] _bits = new ulong[(char.MaxValue + 1) / 64];

        /// <summary>Asks the face about every code point of the plane.</summary>
        /// <param name="family">The family.</param>
        /// <param name="style">The weight.</param>
        public FaceCoverage(string family, XFontStyleEx style)
        {
            var font = new XFont(family, ProbeSize, style);
            for (var codePoint = 0; codePoint <= char.MaxValue; codePoint++)
            {
                if (!char.IsSurrogate((char)codePoint) && GlyphHelper.GlyphIndexFromCodePoint(codePoint, font) != 0)
                {
                    _bits[codePoint >> 6] |= 1UL << codePoint;
                }
            }
        }

        /// <summary>Whether the face has a glyph for a character.</summary>
        /// <param name="rune">The character.</param>
        /// <returns>True when the face can print it.</returns>
        public bool Has(Rune rune) => rune.IsBmp && (_bits[rune.Value >> 6] & (1UL << rune.Value)) != 0;
    }
}
