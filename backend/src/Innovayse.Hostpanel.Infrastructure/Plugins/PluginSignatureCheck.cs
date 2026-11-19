using Hostpanel.Sdk.Packaging.Signing;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>What <see cref="PluginSignatureVerifier.Check"/> decided about one plugin package, from one read of its signature.</summary>
/// <param name="Refusal">The check the package fails and the host refuses it for; <see langword="null"/> when it is admitted.</param>
/// <param name="VerifiedKeyId">
/// The id of the trusted key whose signature the package really carries, whatever the policy admitted; <see langword="null"/>
/// when the package is not signed by a trusted key (a built-in plugin is never read, so it names none either).
/// </param>
public sealed record PluginSignatureCheck(PluginSignatureViolation? Refusal, string? VerifiedKeyId);
