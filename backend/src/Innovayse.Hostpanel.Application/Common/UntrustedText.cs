namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// Makes text that came from outside (an uploaded manifest, a plugin registry reply) safe to show and to write into a
/// one-line audit entry. The one place that does it, so every caller cleans the same way.
/// </summary>
public static class UntrustedText
{
    /// <summary>
    /// Replaces control characters with spaces (a line break would forge a second audit line), trims the ends and caps
    /// the length.
    /// </summary>
    /// <param name="value">The outside text; may be <see langword="null"/>.</param>
    /// <param name="maxLength">The most characters kept.</param>
    /// <returns>The cleaned text; empty for <see langword="null"/> or blank input.</returns>
    public static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].TrimEnd();
    }
}
