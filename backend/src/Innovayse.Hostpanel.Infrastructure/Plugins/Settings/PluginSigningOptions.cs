namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>
/// The public keys the host trusts to sign plugin packages, bound from the <c>PluginSigning</c> section. Public keys
/// only: the matching private keys are held by whoever signs, outside this repository and outside the host.
/// </summary>
/// <remarks>
/// There are two lists so an operator can add keys without touching the shipped ones: configuration arrays merge by
/// index, so a single list would let an operator's first entry replace the maintainers' first entry. The trusted
/// set is both lists together. A package signed with a key in neither list is refused as an unknown key.
/// </remarks>
public sealed class PluginSigningOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "PluginSigning";

    /// <summary>
    /// The maintainers' public keys, shipped with the host in <c>appsettings.json</c>. Empty until the first
    /// maintainer key is published; an operator does not edit this list.
    /// </summary>
    public List<TrustedPluginKey> MaintainerKeys { get; set; } = [];

    /// <summary>The keys an operator adds in their own configuration (<c>PluginSigning__OperatorKeys__0__KeyId</c> and <c>__PublicKey</c>).</summary>
    public List<TrustedPluginKey> OperatorKeys { get; set; } = [];

    /// <summary>The trusted set: the maintainers' keys followed by the operator's.</summary>
    public IReadOnlyList<TrustedPluginKey> TrustedKeys => [.. MaintainerKeys, .. OperatorKeys];
}
