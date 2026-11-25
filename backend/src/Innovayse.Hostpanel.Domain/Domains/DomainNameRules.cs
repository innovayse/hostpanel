namespace Innovayse.Hostpanel.Domain.Domains;

/// <summary>
/// The one rule for what a fully-qualified domain name looks like: its length, the labels it is made of
/// and the characters a label may hold. A use case that takes a name from a visitor asks it, so no two of
/// them can disagree on whether <c>bad name!.com</c> or <c>-bad-.com</c> is a name at all.
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

    /// <summary>Whether a text is a well-formed fully-qualified domain name.</summary>
    /// <param name="name">The name as typed; surrounding white space is not part of a name.</param>
    /// <returns>
    /// <see langword="true"/> when the trimmed text is at most <see cref="MaxNameLength"/> characters, has a name
    /// and an extension, and every label passes <see cref="IsValidLabel"/>; <see langword="false"/> for a null or
    /// blank text.
    /// </returns>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            return false;
        }

        var labels = trimmed.Split(LabelSeparator);
        return labels.Length >= MinLabels && labels.All(IsValidLabel);
    }

    /// <summary>Whether one label of a name is well-formed.</summary>
    /// <param name="label">The text between two dots.</param>
    /// <returns>
    /// <see langword="true"/> when the label is 1 to <see cref="MaxLabelLength"/> characters of letters, digits and
    /// hyphens (a letter of any script, so an internationalised name passes) and neither starts nor ends with a
    /// hyphen.
    /// </returns>
    public static bool IsValidLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        return label.Length is >= 1 and <= MaxLabelLength
            && label[0] != Hyphen
            && label[^1] != Hyphen
            && label.All(c => c == Hyphen || char.IsLetterOrDigit(c));
    }
}
