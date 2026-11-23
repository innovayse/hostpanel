namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// The control panel module ids the host's own code names, spelled exactly as they are stored. A server's module is an open text
/// id, because a panel plugin declares its own ids (<c>serverModules</c> in its manifest); only <see cref="CPanel"/> is built in,
/// so <see cref="Known"/> offers nothing else. <see cref="Linux"/> is an infrastructure type, not a product: listed in the catalog
/// (<see cref="Listed"/>) so an existing server can be added with it, but never orderable. <see cref="Cwp7"/> is a legacy id: not offered, but a server stored with it still
/// maps to its SSH agent profile. Compare two ids with <see cref="Same"/>.
/// </summary>
public static class ServerModules
{
    /// <summary>cPanel/WHM, provisioned by the host itself.</summary>
    public const string CPanel = "CPanel";

    /// <summary>Control Web Panel 7 (legacy, stored rows only; the SSH agent still maps it to its profile).</summary>
    public const string Cwp7 = "Cwp7";

    /// <summary>A plain Linux server with no control panel; the SSH agent serves it with its built-in profile, and orders never pick it.</summary>
    public const string Linux = "Linux";

    /// <summary>The modules the host provisions itself, the only ones offered without an installed panel plugin.</summary>
    public static readonly IReadOnlyList<string> Known = Array.AsReadOnly<string>([CPanel]);

    /// <summary>The modules the catalog lists: <see cref="Known"/> plus <see cref="Linux"/>, which is listed but never orderable.</summary>
    public static readonly IReadOnlyList<string> Listed = Array.AsReadOnly<string>([CPanel, Linux]);

    /// <summary>Tells whether two module ids name the same module: ordinal, ignoring case, the one comparison every caller uses.</summary>
    /// <param name="a">The first id, or <see langword="null"/>.</param>
    /// <param name="b">The second id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are set and equal ignoring case.</returns>
    public static bool Same(string? a, string? b)
    {
        return a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
