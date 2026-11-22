namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// The control panel module ids the host's own code names, spelled exactly as they are stored. A server's module is an open text
/// id, because a panel plugin declares its own ids (<c>serverModules</c> in its manifest); only <see cref="CPanel"/> is built in,
/// so <see cref="Known"/> offers nothing else. <see cref="Centos"/> and <see cref="Cwp7"/> are legacy ids: no longer offered,
/// but a server stored with one keeps the special handling the host still gives it. Compare two ids with <see cref="Same"/>.
/// </summary>
public static class ServerModules
{
    /// <summary>cPanel/WHM, provisioned by the host itself.</summary>
    public const string CPanel = "CPanel";

    /// <summary>CentOS Web Panel (legacy, stored rows only).</summary>
    public const string Centos = "Centos";

    /// <summary>Control Web Panel 7 (legacy, stored rows only; the SSH agent still maps it to its profile).</summary>
    public const string Cwp7 = "Cwp7";

    /// <summary>The modules the host provisions itself, the only ones offered without an installed panel plugin.</summary>
    public static readonly IReadOnlyList<string> Known = Array.AsReadOnly<string>([CPanel]);

    /// <summary>Tells whether two module ids name the same module: ordinal, ignoring case, the one comparison every caller uses.</summary>
    /// <param name="a">The first id, or <see langword="null"/>.</param>
    /// <param name="b">The second id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are set and equal ignoring case.</returns>
    public static bool Same(string? a, string? b)
    {
        return a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
