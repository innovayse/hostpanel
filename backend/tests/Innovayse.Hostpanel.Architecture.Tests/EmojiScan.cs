using System.Text;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Finds emoji in text. The rule is a plain function of named texts, so a test can run it on an in-memory planted text
/// and on the real files.
/// </summary>
internal static class EmojiScan
{
    /// <summary>Whether a character is an emoji or a pictograph: the symbol, dingbat, technical, arrow-emoji and pictograph blocks and the emoji variation selector. Plain punctuation, arrows in prose and letters of every script are not.</summary>
    /// <param name="rune">The character to test.</param>
    /// <returns><see langword="true"/> when the character is an emoji.</returns>
    public static bool IsEmoji(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x1F000 and <= 0x1FAFF
            or >= 0x2600 and <= 0x27BF
            or >= 0x2B00 and <= 0x2BFF
            or >= 0x2300 and <= 0x23FF
            or 0xFE0F;
    }

    /// <summary>The emoji a text contains.</summary>
    /// <param name="text">The text to read.</param>
    /// <returns>Each emoji as a string, in order of appearance.</returns>
    public static IReadOnlyList<string> EmojiIn(string text)
    {
        return text.EnumerateRunes().Where(IsEmoji).Select(rune => rune.ToString()).ToList();
    }

    /// <summary>The named texts that contain an emoji.</summary>
    /// <param name="texts">Pairs of a name for messages and the text.</param>
    /// <returns>One <c>name</c> line per text with an emoji.</returns>
    public static IReadOnlyList<string> Offenders(IEnumerable<(string Name, string Text)> texts)
    {
        return texts.Where(text => EmojiIn(text.Text).Count > 0).Select(text => text.Name).ToList();
    }
}
