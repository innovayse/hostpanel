using System.Globalization;
using Innovayse.Hostpanel.Domain.Plugins.Enums;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// A plugin version in strict semantic version form: <c>major.minor.patch</c>, all three numbers without leading zeros,
/// an optional <c>-pre-release</c> and an optional <c>+build</c> that does not affect precedence. Anything else is not a
/// version (no surrounding space, no missing patch, no overflowing number), so two different texts can never pass as the
/// same version by an accident of lenient parsing. The host compares versions to tell an upgrade from a downgrade; it
/// never interprets them any other way.
/// </summary>
public sealed class PluginVersion : IComparable<PluginVersion>
{
    /// <summary>The dot separated pre-release identifiers, or empty for a release.</summary>
    private readonly string[] _preRelease;

    /// <summary>The major number.</summary>
    public int Major { get; }

    /// <summary>The minor number.</summary>
    public int Minor { get; }

    /// <summary>The patch number.</summary>
    public int Patch { get; }

    /// <summary>Initializes a new instance of the <see cref="PluginVersion"/> class.</summary>
    /// <param name="major">The major number.</param>
    /// <param name="minor">The minor number.</param>
    /// <param name="patch">The patch number.</param>
    /// <param name="preRelease">The pre-release identifiers, empty for a release.</param>
    private PluginVersion(int major, int minor, int patch, string[] preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _preRelease = preRelease;
    }

    /// <summary>Parses a version text.</summary>
    /// <param name="text">The text, for example <c>1.2.3</c> or <c>2.0.0-beta.1+7</c>.</param>
    /// <param name="version">The parsed version when the text is one.</param>
    /// <returns><see langword="true"/> when the text is a strict semantic version.</returns>
    public static bool TryParse(string? text, out PluginVersion version)
    {
        version = null!;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var plus = text.IndexOf('+');
        if (plus >= 0 && !AreIdentifiers(text[(plus + 1)..], numericWithoutLeadingZero: false))
        {
            return false;
        }

        var core = plus < 0 ? text : text[..plus];
        var dash = core.IndexOf('-');
        var preRelease = Array.Empty<string>();
        if (dash >= 0)
        {
            var preText = core[(dash + 1)..];
            if (!AreIdentifiers(preText, numericWithoutLeadingZero: true))
            {
                return false;
            }

            preRelease = preText.Split('.');
            core = core[..dash];
        }

        var numbers = core.Split('.');
        if (numbers.Length != 3)
        {
            return false;
        }

        var parts = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (!IsNumber(numbers[i]) || !int.TryParse(numbers[i], NumberStyles.None, CultureInfo.InvariantCulture, out parts[i]))
            {
                return false;
            }
        }

        version = new PluginVersion(parts[0], parts[1], parts[2], preRelease);
        return true;
    }

    /// <summary>Tells how an uploaded version relates to the installed one.</summary>
    /// <param name="installed">The installed version text.</param>
    /// <param name="uploaded">The uploaded version text.</param>
    /// <returns>
    /// <see cref="PluginVersionChange.Incomparable"/> when either text is not a version: a caller must then treat the
    /// upload like a downgrade, because it cannot be proven to be an upgrade.
    /// </returns>
    public static PluginVersionChange Compare(string? installed, string? uploaded)
    {
        if (!TryParse(installed, out var left) || !TryParse(uploaded, out var right))
        {
            return PluginVersionChange.Incomparable;
        }

        var order = right.CompareTo(left);
        return order switch
        {
            > 0 => PluginVersionChange.Newer,
            < 0 => PluginVersionChange.Older,
            _ => PluginVersionChange.Same,
        };
    }

    /// <summary>Compares two versions by semantic version precedence.</summary>
    /// <param name="other">The version to compare with.</param>
    /// <returns>Negative when this is older, zero when equal in precedence, positive when newer.</returns>
    public int CompareTo(PluginVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = Major.CompareTo(other.Major);
        core = core != 0 ? core : Minor.CompareTo(other.Minor);
        core = core != 0 ? core : Patch.CompareTo(other.Patch);
        if (core != 0)
        {
            return core;
        }

        // A release outranks any pre-release of the same numbers.
        if (_preRelease.Length == 0 || other._preRelease.Length == 0)
        {
            return other._preRelease.Length.CompareTo(_preRelease.Length);
        }

        for (var i = 0; i < Math.Min(_preRelease.Length, other._preRelease.Length); i++)
        {
            var result = CompareIdentifier(_preRelease[i], other._preRelease[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }

    /// <summary>Whether a text is a number without a sign, space or leading zero.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the text is <c>0</c> or digits that do not start with 0.</returns>
    private static bool IsNumber(string text)
    {
        return text.Length > 0 && text.All(char.IsAsciiDigit) && (text.Length == 1 || text[0] != '0');
    }

    /// <summary>Whether a dot separated text is a non-empty list of identifiers made of ASCII letters, digits and hyphen.</summary>
    /// <param name="text">The text after <c>-</c> or <c>+</c>.</param>
    /// <param name="numericWithoutLeadingZero">Whether an identifier of digits only must not start with 0 (pre-release rule).</param>
    /// <returns><see langword="true"/> when every identifier is allowed.</returns>
    private static bool AreIdentifiers(string text, bool numericWithoutLeadingZero)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var identifier in text.Split('.'))
        {
            if (identifier.Length == 0 || !identifier.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                return false;
            }

            if (numericWithoutLeadingZero && identifier.All(char.IsAsciiDigit) && !IsNumber(identifier))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares two pre-release identifiers: numbers numerically and below text, text in ordinal order.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns>The ordering of <paramref name="left"/> against <paramref name="right"/>.</returns>
    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumber = left.All(char.IsAsciiDigit);
        var rightIsNumber = right.All(char.IsAsciiDigit);
        if (leftIsNumber && rightIsNumber)
        {
            // No leading zeros are allowed, so a longer number is the larger one; this cannot overflow.
            return left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);
        }

        if (leftIsNumber != rightIsNumber)
        {
            return leftIsNumber ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }
}
