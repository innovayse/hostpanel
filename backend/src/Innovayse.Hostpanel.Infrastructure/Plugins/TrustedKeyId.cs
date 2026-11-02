using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>The shape of a signing key id, shared by the trusted key configuration, <c>plugin.sig</c> and the signing tool.</summary>
public static partial class TrustedKeyId
{
    /// <summary>
    /// Lower-case letters, digits, dot, hyphen and underscore, 1 to 64 characters, starting with a letter or digit.
    /// Narrow on purpose: the id is written into the signed payload on its own line, so it must never hold a newline
    /// or a space. The end is <c>\z</c>, not <c>$</c>, because <c>$</c> also matches before a trailing newline.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>Whether a text is a valid key id.</summary>
    /// <param name="keyId">The candidate.</param>
    /// <returns><see langword="true"/> when it is 1 to 64 characters of the allowed set.</returns>
    public static bool IsValid(string? keyId)
    {
        return keyId is not null && Pattern().IsMatch(keyId);
    }
}
