using Innovayse.SDK.Plugins;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;

/// <summary>
/// Behaviour switches for how <see cref="ProvisioningProviderFactory"/> talks to control panels,
/// bound from the <c>Provisioning</c> section.
/// </summary>
/// <remarks>
/// <see cref="UseFakeProvider"/> exists because the factory otherwise only knows how to build a
/// provider that makes real HTTP calls to a live CWP7 panel. A developer running the stack has
/// no such panel, so setting up a service, the last step of the order flow, cannot complete and
/// the whole path is untestable. With it on, the factory returns the in-process no-op provider
/// instead, so "Build My Server" runs end to end against seeded data. It defaults off: every real
/// environment provisions for real.
/// </remarks>
public sealed class ProvisioningOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Provisioning";

    /// <summary>The port a CWP7 server's user-facing panel answers on when none is configured.</summary>
    public const int DefaultCwp7UserPanelPort = 2083;

    /// <summary>The port a Plesk server answers both its XML API and its panel on when none is configured.</summary>
    public const int DefaultPleskPort = 8443;

    /// <summary>
    /// When <see langword="true"/>, the provider factory returns an in-process provider that
    /// reports success without contacting any external control panel. Development and testing
    /// only; must stay <see langword="false"/> in stage and production, where a service marked
    /// active would otherwise have no account behind it.
    /// </summary>
    public bool UseFakeProvider { get; set; }

    /// <summary>
    /// The port every CWP7 server answers its REST API on. Servers carry no port of their own,
    /// so one value serves them all; the default is the number the panel contract declares.
    /// </summary>
    public int Cwp7ApiPort { get; set; } = IHostingPanelApiClient.DefaultApiPort;

    /// <summary>
    /// The port every CWP7 server's user-facing panel answers on; the sign-in address shown to an
    /// account owner is built from it.
    /// </summary>
    public int Cwp7UserPanelPort { get; set; } = DefaultCwp7UserPanelPort;

    /// <summary>
    /// The port every Plesk server answers on. Plesk serves the XML API and the panel a customer
    /// signs in to on the same HTTPS port, so one value builds both addresses.
    /// </summary>
    public int PleskPort { get; set; } = DefaultPleskPort;
}
