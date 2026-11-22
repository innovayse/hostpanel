using System.Reflection;

namespace Innovayse.Hostpanel.Application.Common.Errors;

/// <summary>
/// The error codes a hosting panel plugin's answer can end in: every <see cref="HostpanelErrorCodes"/> constant whose name starts with
/// <see cref="Prefix"/>. A handler that calls a panel answers one of these with its own sentence and every other failure with its
/// generic "upstream failed" code. The family is read from the constants themselves, so a code added to it is in the set by being
/// declared, with no second list to keep in step.
/// </summary>
public static class PanelErrorCodes
{
    /// <summary>The name prefix every code of the panel family carries.</summary>
    public const string Prefix = "PANEL_";

    /// <summary>Every panel code, one entry each.</summary>
    private static readonly IReadOnlySet<string> All = typeof(HostpanelErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string) && field.Name.StartsWith(Prefix, StringComparison.Ordinal))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Tells whether a code belongs to the panel family.</summary>
    /// <param name="code">The error code of a refusal.</param>
    /// <returns><see langword="true"/> when the code is one a panel plugin's answer can end in.</returns>
    public static bool Contains(string code)
    {
        return All.Contains(code);
    }
}
