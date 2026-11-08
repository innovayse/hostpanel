namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one rule for what a registry licence key may look like: at most 256 printable ASCII characters ('!' to '~'), after
/// trimming. The key travels in a request header, so a space, a control character or a non-ASCII character cannot be part
/// of it. The save validator and the registry client both use this class, so the two can never disagree.
/// </summary>
public static class RegistryLicenceKeyRule
{
    /// <summary>The most characters a licence key may have, after trimming.</summary>
    public const int MaxLength = 256;

    /// <summary>Whether a trimmed key is short enough.</summary>
    /// <param name="key">The key, already trimmed.</param>
    /// <returns><see langword="true"/> when it is within <see cref="MaxLength"/>.</returns>
    public static bool IsShortEnough(string key)
    {
        return key.Length <= MaxLength;
    }

    /// <summary>Whether every character of a trimmed key is printable ASCII ('!' to '~').</summary>
    /// <param name="key">The key, already trimmed.</param>
    /// <returns><see langword="true"/> when no character is a space, a control character or outside ASCII.</returns>
    public static bool IsPrintableAscii(string key)
    {
        return key.All(c => c is >= '!' and <= '~');
    }

    /// <summary>Whether a key obeys the whole rule once trimmed.</summary>
    /// <param name="key">The key as the caller has it.</param>
    /// <returns><see langword="true"/> when the trimmed key is non-empty, short enough and printable ASCII.</returns>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var trimmed = key.Trim();
        return IsShortEnough(trimmed) && IsPrintableAscii(trimmed);
    }
}
