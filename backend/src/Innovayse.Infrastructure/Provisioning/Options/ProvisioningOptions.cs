namespace Innovayse.Infrastructure.Provisioning.Options;

/// <summary>
/// Behaviour switches for how <see cref="ProvisioningProviderFactory"/> talks to control panels,
/// bound from the <c>Provisioning</c> section.
/// </summary>
/// <remarks>
/// The one setting here exists because the factory otherwise only knows how to build a provider
/// that makes real HTTP calls to a live CWP7 panel on port 2304. A developer running the stack has
/// no such panel, so setting up a service — the last step of the order flow — cannot complete and
/// the whole path is untestable. With <see cref="UseFakeProvider"/> on, the factory returns the
/// in-process no-op provider instead, so "Build My Server" runs end to end against seeded data.
/// It defaults off: every real environment provisions for real.
/// </remarks>
public sealed class ProvisioningOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Provisioning";

    /// <summary>
    /// When <see langword="true"/>, the provider factory returns an in-process provider that
    /// reports success without contacting any external control panel. Development and testing
    /// only; must stay <see langword="false"/> in stage and production, where a service marked
    /// active would otherwise have no account behind it.
    /// </summary>
    public bool UseFakeProvider { get; set; }
}
