namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>Small text helpers shared by handlers that turn a value as written into the value as answered.</summary>
public static class StringExtensions
{
    /// <summary>Treats a blank value as an absent one.</summary>
    /// <param name="value">The value as written.</param>
    /// <returns>The value, or <see langword="null"/> when it is <see langword="null"/>, empty or white space.</returns>
    public static string? NullIfBlank(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
