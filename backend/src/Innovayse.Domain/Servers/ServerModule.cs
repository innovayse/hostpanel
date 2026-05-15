namespace Innovayse.Domain.Servers;

/// <summary>
/// Identifies the control panel module used to manage a server.
/// </summary>
public enum ServerModule
{
    /// <summary>cPanel/WHM provisioning module.</summary>
    CPanel,

    /// <summary>Plesk provisioning module.</summary>
    Plesk,

    /// <summary>DirectAdmin provisioning module.</summary>
    DirectAdmin,

    /// <summary>CentOS Web Panel (CWP7) provisioning module.</summary>
    Cwp7,

    /// <summary>Lukittu license server module.</summary>
    Lukittu,

    /// <summary>Innovayse WordPress Hosting module.</summary>
    InnovayseWordPressHosting,

    /// <summary>Virtualmin provisioning module.</summary>
    Virtualmin,

    /// <summary>HyperVM VPS provisioning module.</summary>
    HyperVm,
}
