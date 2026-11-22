namespace Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;

/// <summary>How many installed plugins serve a server module for one kind of work.</summary>
public enum ServerModulePluginOutcome
{
    /// <summary>No installed plugin declares the module for that work; the host's own provider, if any, handles it.</summary>
    NotServed = 0,

    /// <summary>Exactly one installed plugin declares the module for that work and serves it.</summary>
    Served = 1,

    /// <summary>Two or more installed plugins declare the module for that work; none of them is chosen.</summary>
    Conflict = 2,
}
