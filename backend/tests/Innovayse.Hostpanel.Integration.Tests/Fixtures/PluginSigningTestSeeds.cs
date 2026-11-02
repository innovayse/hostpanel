using System.IO.Compression;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Signing helpers for the plugin signature tests: two key pairs generated fresh for every test run (never stored,
/// never a real key), a verifier that trusts the first, and builders for signed and tampered packages.
/// </summary>
internal static class PluginSigningTestSeeds
{
    /// <summary>The id of the key the test verifier trusts.</summary>
    public const string TrustedKeyId = "test-key";

    /// <summary>The id of a key the test verifier does not know.</summary>
    public const string UntrustedKeyId = "other-key";

    /// <summary>The trusted key pair, made for this run only.</summary>
    public static readonly (byte[] PrivateKey, byte[] PublicKey) TrustedKey = PluginSigner.GenerateKeyPair();

    /// <summary>A key pair the verifier does not trust, made for this run only.</summary>
    public static readonly (byte[] PrivateKey, byte[] PublicKey) UntrustedKey = PluginSigner.GenerateKeyPair();

    /// <summary>The trusted key as the host's configuration lists it.</summary>
    public static TrustedPluginKey TrustedEntry => new()
    {
        KeyId = TrustedKeyId,
        PublicKey = Convert.ToBase64String(TrustedKey.PublicKey),
    };

    /// <summary>A verifier that trusts <see cref="TrustedKey"/>.</summary>
    /// <param name="enforced"><see langword="true"/> for a Production host, <see langword="false"/> for Development.</param>
    /// <returns>The verifier.</returns>
    public static PluginSignatureVerifier Verifier(bool enforced)
    {
        return new PluginSignatureVerifier([TrustedEntry], enforced);
    }

    /// <summary>Makes a new empty folder under the system temp folder.</summary>
    /// <param name="label">A short label for the folder name.</param>
    /// <returns>The absolute folder; the caller deletes it.</returns>
    public static string NewFolder(string label)
    {
        return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"{label}-{Guid.NewGuid():N}")).FullName;
    }

    /// <summary>Writes a small package: a manifest and one harmless file, plus any further files.</summary>
    /// <param name="folder">The package root; created when missing.</param>
    /// <param name="pluginId">The plugin id to write into the manifest.</param>
    /// <param name="extraFiles">Further files, relative path (forward slashes) to text; may be null.</param>
    public static void WritePackage(string folder, string pluginId, IReadOnlyDictionary<string, string>? extraFiles = null)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), PluginsTestSeeds.ManifestJson(pluginId));
        File.WriteAllText(Path.Combine(folder, "readme.txt"), "harmless file");
        foreach (var (relative, text) in extraFiles ?? new Dictionary<string, string>())
        {
            var path = Path.Combine(folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
    }

    /// <summary>Signs the package in a folder with the trusted key, or with the given one.</summary>
    /// <param name="folder">The package root.</param>
    /// <param name="keyId">The key id to sign under.</param>
    /// <param name="privateKey">The private key; the trusted one when null.</param>
    public static void Sign(string folder, string keyId = TrustedKeyId, byte[]? privateKey = null)
    {
        PluginSigner.Sign(folder, keyId, privateKey ?? TrustedKey.PrivateKey);
    }

    /// <summary>Zips a package folder the way an uploader would.</summary>
    /// <param name="folder">The package root.</param>
    /// <returns>The archive's raw bytes.</returns>
    public static byte[] Zip(string folder)
    {
        using var stream = new MemoryStream();
        ZipFile.CreateFromDirectory(folder, stream, CompressionLevel.Fastest, includeBaseDirectory: false);
        return stream.ToArray();
    }

    /// <summary>Builds a signed plugin archive, optionally changing a file after it is signed.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="keyId">The key id to sign under.</param>
    /// <param name="privateKey">The private key; the trusted one when null.</param>
    /// <param name="afterSigning">Edits the folder after it is signed, to tamper with it; may be null.</param>
    /// <returns>The archive's raw bytes.</returns>
    public static byte[] SignedZip(
        string pluginId, string keyId = TrustedKeyId, byte[]? privateKey = null, Action<string>? afterSigning = null)
    {
        var folder = NewFolder("signed-zip");
        try
        {
            WritePackage(folder, pluginId);
            Sign(folder, keyId, privateKey);
            afterSigning?.Invoke(folder);
            return Zip(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
