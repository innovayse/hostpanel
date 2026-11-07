using Hostpanel.Sdk.Packaging.Signing;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one place the host checks the detached signature of a plugin package. Installing an upload
/// (<see cref="PluginPackageStore"/>) and loading a plugin at startup (<see cref="PluginLoader"/>) both ask it, so
/// the rule is written once and holds on both paths, next to <see cref="PluginManifestValidator"/>.
/// </summary>
/// <remarks>
/// <para>
/// A package is signed when it has a <c>plugin.sig</c> naming one of the trusted keys and the Ed25519 signature
/// matches <see cref="PluginSignaturePayload"/>. <see cref="Inspect"/> answers that and nothing else, through the
/// plugin packaging package's <see cref="PluginSignatureValidator"/>, the code that also signs and checks a package
/// for a plugin author; <see cref="Check"/> adds the policy of the host, which the package does not have.
/// </para>
/// <para>
/// The policy: a plugin shipped inside the host image (<see cref="PluginOrigin.BuiltIn"/>) is never checked, because
/// it is part of the host and exactly as trustworthy as the image. An uploaded plugin is checked only on a host that
/// enforces signatures, which is every environment except Development and Testing, and is admitted despite a failed
/// check only when the admin setting "allow unsigned plugins" is on. The archive SHA-256 pin
/// (<see cref="PluginOptions.TrustedPackageSha256"/>) is a separate, older control and stays as it was.
/// </para>
/// </remarks>
public sealed class PluginSignatureVerifier
{
    /// <summary>The trusted public keys by key id; a configured key that cannot be decoded is absent (startup validation reports it).</summary>
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    /// <summary>Initializes the verifier.</summary>
    /// <param name="trustedKeys">The trusted public keys.</param>
    /// <param name="enforced">Whether signatures are enforced: <see langword="false"/> in Development and Testing.</param>
    public PluginSignatureVerifier(IEnumerable<TrustedPluginKey> trustedKeys, bool enforced)
    {
        ArgumentNullException.ThrowIfNull(trustedKeys);

        Enforced = enforced;
        foreach (var key in trustedKeys)
        {
            if (key is not null && TrustedKeyId.IsValid(key.KeyId) && key.TryDecode(out var publicKey))
            {
                _keys[key.KeyId] = publicKey;
            }
        }
    }

    /// <summary>Whether this host enforces signatures on uploaded plugins.</summary>
    public bool Enforced { get; }

    /// <summary>Builds the verifier a host uses: signatures are enforced everywhere except Development and Testing.</summary>
    /// <param name="options">The trusted keys.</param>
    /// <param name="environment">The host environment, which decides whether signatures are enforced.</param>
    /// <returns>The verifier.</returns>
    public static PluginSignatureVerifier Create(PluginSigningOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var relaxed = environment.IsDevelopment() || environment.IsEnvironment(EnvironmentNames.Testing);
        return new PluginSignatureVerifier(options.TrustedKeys, enforced: !relaxed);
    }

    /// <summary>
    /// Decides whether a plugin folder may be installed or loaded, and logs the reason when it is admitted despite a
    /// failed check.
    /// </summary>
    /// <param name="pluginDir">The plugin folder: the package root.</param>
    /// <param name="pluginId">The plugin id, for the log.</param>
    /// <param name="origin">Where the plugin comes from.</param>
    /// <param name="allowUnsigned">Whether the admin setting "allow unsigned plugins" is on.</param>
    /// <param name="logger">Records an admission despite a failed check.</param>
    /// <param name="require">
    /// Whether the signature is checked even on a host that does not enforce signatures (Development, Testing). Set for a
    /// package fetched from the plugin registry; "allow unsigned" does not override it.
    /// </param>
    /// <returns>The check the plugin fails and the host refuses it for, or <see langword="null"/> when it is admitted.</returns>
    public PluginSignatureViolation? Check(
        string pluginDir, string pluginId, PluginOrigin origin, bool allowUnsigned, ILogger logger, bool require = false)
    {
        ArgumentNullException.ThrowIfNull(logger);

        // The host's own files: authenticity is that of the image they shipped in.
        if (origin == PluginOrigin.BuiltIn || (!Enforced && !require))
        {
            return null;
        }

        var violation = Inspect(pluginDir);
        if (violation is not null && allowUnsigned && !require)
        {
            logger.LogWarning(
                "Plugin {PluginId} fails the signature check ({Rule}: {Detail}) and is admitted because allow unsigned plugins is on.",
                pluginId, violation.Rule, violation.Detail);
            return null;
        }

        return violation;
    }

    /// <summary>Checks the signature of the package in a folder against the trusted keys, whatever the policy.</summary>
    /// <param name="pluginDir">The plugin folder: the package root.</param>
    /// <returns>The check the package fails, or <see langword="null"/> when it is signed by a trusted key and unchanged since.</returns>
    public PluginSignatureViolation? Inspect(string pluginDir)
    {
        return PluginSignatureValidator.Verify(pluginDir, _keys);
    }
}
