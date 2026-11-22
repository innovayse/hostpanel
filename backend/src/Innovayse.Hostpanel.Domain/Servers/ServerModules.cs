namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// The control panel module ids the host itself knows, spelled exactly as they are stored. A server's module is an open text
/// id, because a panel plugin declares its own ids (<c>serverModules</c> in its manifest); these constants name the ids the
/// host's own code and the rows saved before modules became text refer to. Compare two ids with <see cref="Same"/>.
/// </summary>
public static class ServerModules
{
    /// <summary>cPanel/WHM, provisioned by the host itself.</summary>
    public const string CPanel = "CPanel";

    /// <summary>DirectAdmin.</summary>
    public const string DirectAdmin = "DirectAdmin";

    /// <summary>CentOS Web Panel (legacy).</summary>
    public const string Centos = "Centos";

    /// <summary>Control Web Panel 7.</summary>
    public const string Cwp7 = "Cwp7";

    /// <summary>Lukittu license server.</summary>
    public const string Lukittu = "Lukittu";

    /// <summary>Virtualmin.</summary>
    public const string Virtualmin = "Virtualmin";

    /// <summary>HyperVM VPS.</summary>
    public const string HyperVm = "HyperVm";

    /// <summary>The ids the server form offered before modules became open text, in that order.</summary>
    public static readonly IReadOnlyList<string> Known =
        Array.AsReadOnly<string>([CPanel, DirectAdmin, Centos, Cwp7, Lukittu, Virtualmin, HyperVm]);

    /// <summary>Tells whether two module ids name the same module: ordinal, ignoring case, the one comparison every caller uses.</summary>
    /// <param name="a">The first id, or <see langword="null"/>.</param>
    /// <param name="b">The second id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are set and equal ignoring case.</returns>
    public static bool Same(string? a, string? b)
    {
        return a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
