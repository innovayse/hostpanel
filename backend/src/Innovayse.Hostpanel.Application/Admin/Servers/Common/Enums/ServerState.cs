namespace Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;

/// <summary>
/// The last known connection state of a server, as the server list filters by it. It is read from the outcome of the
/// last connection test (<c>Server.IsOnline</c>), so a server nobody has tested yet is a state of its own.
/// </summary>
public enum ServerState
{
    /// <summary>The last connection test succeeded.</summary>
    Online = 0,

    /// <summary>The last connection test failed.</summary>
    Offline = 1,

    /// <summary>The server has never been tested.</summary>
    Untested = 2,
}
