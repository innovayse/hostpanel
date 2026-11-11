using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Reads the <c>permission</c> a plugin manifest declares on a client route or a client slot widget, one way for both:
/// the route check of a plugin call and the widget filter of a slot index must agree on who may see what.
/// </summary>
public static class PluginClientPermissions
{
    /// <summary>The permission a declared <c>permission</c> member needs.</summary>
    /// <param name="permission">The declared member; <see langword="null"/> when the manifest names none.</param>
    /// <returns>
    /// <see cref="ClientPermission.None"/> for <see cref="PluginPermissions.Any"/>, the named flag for a defined permission,
    /// and <see cref="ClientPermission.All"/> for none declared or a name nobody defined: like every portal action, an
    /// entry that does not say who may use it is for the account holder alone, never for everyone.
    /// </returns>
    public static ClientPermission Required(string? permission)
    {
        if (permission == PluginPermissions.Any)
        {
            return ClientPermission.None;
        }

        // IsDefined on a string matches defined names only, so "7" or "ViewDomains, ViewEmails" never parse.
        return permission is not null
            && Enum.IsDefined(typeof(ClientPermission), permission)
            && Enum.TryParse<ClientPermission>(permission, out var parsed)
            && parsed != ClientPermission.None
                ? parsed
                : ClientPermission.All;
    }

    /// <summary>Whether the held permissions cover what an entry needs.</summary>
    /// <param name="held">The permissions the caller holds on the account.</param>
    /// <param name="permission">The declared <c>permission</c> member.</param>
    /// <returns><see langword="true"/> when every flag <see cref="Required"/> asks for is held.</returns>
    public static bool IsHeld(ClientPermission held, string? permission)
    {
        var needed = Required(permission);
        return (held & needed) == needed;
    }
}
