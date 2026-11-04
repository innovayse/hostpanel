using Hostpanel.Sdk.Packaging.Manifest;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one place the host checks a plugin manifest and the assemblies beside it against the manifest rules.
/// Loading a plugin at startup (<see cref="PluginManifestReader.TryRead(string, out PluginManifestViolation?)"/>) and installing an upload
/// (<see cref="PluginPackageStore"/>) both ask it, so a rule is written once and holds on both paths.
/// </summary>
/// <remarks>
/// The structural rules (versions, ids, permissions, route and page paths, slots, menu icons, text keys) are the
/// plugin packaging package's <see cref="PluginManifestChecker"/>, the same code a plugin author runs before
/// publishing. What the host adds is what only it knows: the permission names its clients can hold and the manifest
/// versions it reads. The package reports its own rule enum; <see cref="PluginRuleMap"/> turns it into the host's.
/// </remarks>
internal static class PluginManifestValidator
{
    /// <summary>
    /// What the host decides for the package's rules: the manifest versions it reads (the package's default, <c>1</c> and
    /// <c>2</c>) and the permission names a client route, page or slot entry may name, every defined
    /// <see cref="ClientPermission"/> except <see cref="ClientPermission.None"/> (a permission that requires nothing is
    /// written <see cref="PluginPermissions.Any"/>), compared exactly.
    /// </summary>
    private static readonly PluginManifestOptions Options = new()
    {
        ClientPermissionNames = Enum.GetNames<ClientPermission>()
            .Where(name => name != nameof(ClientPermission.None))
            .ToHashSet(StringComparer.Ordinal),
    };

    /// <summary>
    /// Checks the manifest alone: its version, permissions, route paths, text keys and slots. A version 1 manifest
    /// declares none of the blocks these rules look at, so it passes by having nothing to break.
    /// </summary>
    /// <param name="manifest">The manifest, already read.</param>
    /// <returns>The first rule the manifest breaks, or <see langword="null"/> when it meets them all.</returns>
    internal static PluginManifestViolation? Validate(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var violation = PluginManifestChecker.Validate(manifest, Options);
        return violation is null ? null : PluginRuleMap.ToHost(violation);
    }

    /// <summary>
    /// Checks the assemblies in a plugin folder: one built against an older SDK major version than the host's is
    /// refused up front, because loading it would fail with a type load exception that names nothing useful. The
    /// assembly metadata is read without loading it, so the plugin never reaches the default load context.
    /// </summary>
    /// <param name="pluginDirectory">The folder holding the plugin's assemblies.</param>
    /// <returns>The violation naming the first outdated or unreadable assembly, or <see langword="null"/> when none is.</returns>
    internal static PluginManifestViolation? ValidateSdkReference(string pluginDirectory)
    {
        var violation = PluginManifestChecker.ValidateSdkReference(pluginDirectory);
        return violation is null ? null : PluginRuleMap.ToHost(violation);
    }

    /// <summary>Gets the English texts of a manifest, the locale that must define every text key.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The texts by key, or <see langword="null"/> when the manifest ships no English locale.</returns>
    internal static IReadOnlyDictionary<string, string>? EnglishTexts(PluginManifest manifest)
    {
        return PluginManifestChecker.EnglishTexts(manifest);
    }

    /// <summary>Tells whether a label value is shaped like a text key rather than plain text.</summary>
    /// <param name="value">The label value.</param>
    /// <returns><see langword="true"/> when the value has the shape of a text key.</returns>
    internal static bool IsTextKey(string value)
    {
        return PluginManifestChecker.IsTextKey(value);
    }

    /// <summary>Checks that a value taken as a text key is defined in the English texts; the one rule manifest and view labels share.</summary>
    /// <param name="where">Where the value is written, for the log.</param>
    /// <param name="key">The text key.</param>
    /// <param name="english">The English texts, or <see langword="null"/> when there are none.</param>
    /// <returns>The violation, or <see langword="null"/> when the key is defined.</returns>
    internal static PluginManifestViolation? CheckKeyDefined(string where, string key, IReadOnlyDictionary<string, string>? english)
    {
        var violation = PluginManifestChecker.CheckKeyDefined(where, key, english);
        return violation is null ? null : PluginRuleMap.ToHost(violation);
    }
}
