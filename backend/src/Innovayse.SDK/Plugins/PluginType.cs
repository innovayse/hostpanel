namespace Innovayse.SDK.Plugins;

/// <summary>
/// Categorises the service a plugin provides.
/// </summary>
public enum PluginType
{
    /// <summary>Hosting / server provisioning (cPanel, Plesk, CWP, etc.).</summary>
    Provisioning,

    /// <summary>Payment gateway (Stripe, PayPal, etc.).</summary>
    Payment,

    /// <summary>Domain registrar (Namecheap, ENOM, etc.).</summary>
    Registrar,
}
