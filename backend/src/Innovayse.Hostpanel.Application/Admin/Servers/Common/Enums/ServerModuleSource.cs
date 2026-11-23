namespace Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;

/// <summary>Where a control panel module an administrator can pick for a server comes from.</summary>
public enum ServerModuleSource
{
    /// <summary>The host's own code or a stored legacy id.</summary>
    BuiltIn = 0,

    /// <summary>An installed plugin declares it in its manifest <c>serverModules</c>.</summary>
    Plugin = 1,
}
