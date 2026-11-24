namespace Innovayse.Hostpanel.Infrastructure.Plugins.Enums;

/// <summary>
/// Why a panel plugin call failed, as far as the host can tell without reading the plugin's own text. The host sees only the kind
/// of failure, never its message, so no panel answer or secret travels with it. Values start at 1; 0 is not a failure.
/// </summary>
public enum PanelPluginFailure
{
    /// <summary>The plugin threw, or the call failed in a way the host cannot tell apart from any other.</summary>
    Failed = 1,

    /// <summary>The plugin gave no answer within the call time limit, or reported a timeout.</summary>
    TimedOut = 2,

    /// <summary>The secure connection to the server could not be set up: the certificate was refused or the handshake failed.</summary>
    TlsRejected = 3,
}
