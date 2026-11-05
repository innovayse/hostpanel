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

    /// <summary>
    /// The keys an operator adds in their own configuration (<c>PluginSigning__OperatorKeys__0__KeyId</c> and <c>__PublicKey</c>).
    /// A compose file that passes both variables empty leaves one entry with a blank key id and a blank public key;
    /// <see cref="TrustedKeys"/> leaves such an entry out, so an unset pair is not an error.
    /// </summary>
    public List<TrustedPluginKey> OperatorKeys { get; set; } = [];

    /// <summary>
    /// The trusted set: the maintainers' keys followed by the operator's, without an entry whose key id and public key
    /// are both blank. An entry with only one of the two set stays in the set, so the validator still refuses it.
    /// </summary>
    public IReadOnlyList<TrustedPluginKey> TrustedKeys => [.. MaintainerKeys.Where(key => !IsUnset(key)), .. OperatorKeys.Where(key => !IsUnset(key))];

    /// <summary>Whether an entry is the empty pair that an unset deployment variable leaves behind.</summary>
    /// <param name="key">The entry, which may be null.</param>
    /// <returns>True when both the key id and the public key are null, empty or white space.</returns>
    private static bool IsUnset(TrustedPluginKey? key)
    {
        return key is not null && string.IsNullOrWhiteSpace(key.KeyId) && string.IsNullOrWhiteSpace(key.PublicKey);
    }
}
