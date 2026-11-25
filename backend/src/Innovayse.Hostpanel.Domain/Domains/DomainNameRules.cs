namespace Innovayse.Hostpanel.Domain.Domains;

/// <summary>
/// The one rule for what a fully-qualified domain name looks like: its length, the labels it is made of
/// and the characters a label may hold. A use case that takes a name from a visitor asks it, so no two of
/// them can disagree on whether <c>bad name!.com</c> or <c>-bad-.com</c> is a name at all. It is the rule the
/// registration path has always held: ASCII only (an internationalised name is written in its <c>xn--</c> form
/// before it is ordered, no step between the form and the registrar converts it), and an extension of letters.
/// </summary>
public static class DomainNameRules
{
    /// <summary>Longest a whole name may be, dots included.</summary>
    public const int MaxNameLength = 253;

    /// <summary>Longest a single label (the text between two dots) may be.</summary>
    public const int MaxLabelLength = 63;

    /// <summary>The character that separates two labels.</summary>
    private const char LabelSeparator = '.';

    /// <summary>The one punctuation mark a label may hold, and never at its start or end.</summary>
    private const char Hyphen = '-';

    /// <summary>The fewest labels a name needs: one for the name and one for the extension.</summary>
    private const int MinLabels = 2;

    /// <summary>The fewest letters an extension (the last label) needs.</summary>
    private const int MinExtensionLength = 2;

    /// <summary>Whether a text is a well-formed fully-qualified domain name.</summary>
    /// <param name="name">The name exactly as given: white space around it is not part of a name, so a caller that
    /// accepts a padded input trims it first.</param>
    /// <returns>
    /// <see langword="true"/> when the text is at most <see cref="MaxNameLength"/> characters, has a name, every
    /// label passes <see cref="IsValidLabel"/> and the extension passes <see cref="IsValidExtension"/>;
    /// <see langword="false"/> for a null or blank text.
    /// </returns>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return false;
        }

        var labels = name.Split(LabelSeparator);
        return labels.Length >= MinLabels && labels.All(IsValidLabel) && IsValidExtension(labels[^1]);
    }

    /// <summary>Whether one label of a name is well-formed.</summary>
    /// <param name="label">The text between two dots.</param>
    /// <returns>
    /// <see langword="true"/> when the label is 1 to <see cref="MaxLabelLength"/> ASCII letters, digits and
    /// hyphens and neither starts nor ends with a hyphen.
    /// </returns>
    public static bool IsValidLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        return label.Length is >= 1 and <= MaxLabelLength
            && label[0] != Hyphen
            && label[^1] != Hyphen
            && label.All(c => c == Hyphen || char.IsAsciiLetterOrDigit(c));
    }

    /// <summary>Whether the last label of a name is an extension (a top-level domain).</summary>
    /// <param name="label">The last label of a name.</param>
    /// <returns>
    /// <see langword="true"/> when it is at least <see cref="MinExtensionLength"/> ASCII letters: a number is no
    /// extension, so <c>example.123</c> is never a name.
    /// </returns>
    public static bool IsValidExtension(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        return label.Length >= MinExtensionLength && label.All(char.IsAsciiLetter);
    }
}
