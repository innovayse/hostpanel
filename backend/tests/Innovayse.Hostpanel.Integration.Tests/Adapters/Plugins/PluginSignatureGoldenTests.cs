using System.Security.Cryptography;
using System.Text;
using Hostpanel.Sdk.Packaging.Enums;
using Hostpanel.Sdk.Packaging.Signing;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The golden check between the host and the plugin packaging package: for the same files, the host's signing and
/// verification path works on exactly the payload the package builds, and that payload is the documented v1 text built
/// here by hand, independently of the package code. If the package ever changed the payload, a package signed by a
/// plugin author's tool and a package the host verifies would stop agreeing; this is where that shows. No container or
/// database.
/// </summary>
public sealed class PluginSignatureGoldenTests
{
    /// <summary>The payload text the SDK's golden test asserts for its golden package (key id demo-key), copied verbatim.</summary>
    private const string SdkGoldenPayload =
        "innovayse-plugin-signature-v1\n"
        + "key demo-key\n"
        + "ae8f5080a348fbfeb2c776957979728034cdf3eee0160c8667ee66daa8440a48 Zed.txt\n"
        + "d4dc56669143034f31aa309635d4113d9ad76a02b1739da22c965ed2049be9e6 assets/icon.svg\n"
        + "9a3a45d01531a20e89ac6ae10b0b0beb0492acd7216a368aa062d1a5fecaf9cd lib/Demo.dll\n"
        + "2b67bf9bbf4bbcaf60ea7df1f970eee71425d927e00edb4a1de3ddc28ecc0991 plugin.json\n";

    /// <summary>The first line of the payload, spelled out so a change of the format in the package is noticed here.</summary>
    private const string GoldenHeader = "innovayse-plugin-signature-v1";

    /// <summary>
    /// The package builds the payload the format documents: header, key line, then one line per file with its SHA-256 in
    /// lower-case hex, a space and the forward-slash path, sorted ordinally, the root signature file left out.
    /// </summary>
    [Fact]
    public void PackagePayload_IsTheDocumentedTextBuiltByHand()
    {
        var folder = PluginSigningTestSeeds.NewFolder("golden-payload");
        try
        {
            WriteGoldenPackage(folder);

            var payload = Encoding.UTF8.GetString(PluginSignaturePayload.Build(folder, PluginSigningTestSeeds.TrustedKeyId));

            payload.Should().Be(ExpectedPayload(folder, PluginSigningTestSeeds.TrustedKeyId));
            payload.Should().StartWith($"{GoldenHeader}\nkey {PluginSigningTestSeeds.TrustedKeyId}\n");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// The payload of the package the SDK's own golden test uses is exactly the text that test asserts, character for
    /// character, so the host and the package are pinned to one literal.
    /// </summary>
    [Fact]
    public void PackagePayload_OfTheSdkGoldenPackage_IsTheSdkGoldenText()
    {
        var folder = PluginSigningTestSeeds.NewFolder("golden-sdk");
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "lib"));
            Directory.CreateDirectory(Path.Combine(folder, "assets"));
            File.WriteAllText(Path.Combine(folder, "plugin.json"), "{\"id\":\"demo\"}");
            File.WriteAllText(Path.Combine(folder, "lib", "Demo.dll"), "binary");
            File.WriteAllText(Path.Combine(folder, "assets", "icon.svg"), "<svg/>");
            File.WriteAllText(Path.Combine(folder, "Zed.txt"), "zed");
            File.WriteAllText(Path.Combine(folder, PluginSignatureFile.FileName), "{\"keyId\":\"demo-key\",\"signature\":\"ignored\"}");

            Encoding.UTF8.GetString(PluginSignaturePayload.Build(folder, "demo-key")).Should().Be(SdkGoldenPayload);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A package signed over the hand-built payload with the trusted key is accepted by the host's verifier, and the
    /// signature a publisher's signing call writes verifies against that same hand-built payload: the signing side, the
    /// host's verifying side and the documented text all agree for the same files.
    /// </summary>
    [Fact]
    public void HostVerification_AndPackageSigning_AgreeOnTheHandBuiltPayload()
    {
        var folder = PluginSigningTestSeeds.NewFolder("golden-agree");
        try
        {
            WriteGoldenPackage(folder);
            PluginSigningTestSeeds.Sign(folder);
            var expected = Encoding.UTF8.GetBytes(ExpectedPayload(folder, PluginSigningTestSeeds.TrustedKeyId));
            var written = PluginSignatureFile.Parse(File.ReadAllText(Path.Combine(folder, PluginSignatureFile.FileName)));

            PluginSignatureValidator.VerifyPayload(expected, written.Signature, PluginSigningTestSeeds.TrustedKey.PublicKey)
                .Should().BeTrue("the signature covers exactly the documented payload");
            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A signature over a payload that differs from the documented one by a single file line is refused by the host, so
    /// the host cannot be made to accept a package whose payload the package code would build differently.
    /// </summary>
    [Fact]
    public void HostVerification_RefusesASignatureOverADifferentPayload()
    {
        var folder = PluginSigningTestSeeds.NewFolder("golden-different");
        try
        {
            WriteGoldenPackage(folder);
            PluginSigningTestSeeds.Sign(folder);
            File.AppendAllText(Path.Combine(folder, "sub", "data.txt"), "!");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Writes a package with a manifest, a root file, a nested file and a nested signature file, whose names sort in an order a culture-aware sort would change.</summary>
    /// <param name="folder">The package root.</param>
    private static void WriteGoldenPackage(string folder)
    {
        PluginSigningTestSeeds.WritePackage(
            folder,
            "golden-plugin",
            new Dictionary<string, string>
            {
                ["B-upper.txt"] = "upper",
                ["a-lower.txt"] = "lower",
                ["sub/data.txt"] = "nested",
                ["sub/plugin.sig"] = "a nested signature file is part of the payload",
            });
    }

    /// <summary>Builds the documented v1 payload text by hand, without the package code.</summary>
    /// <param name="folder">The package root.</param>
    /// <param name="keyId">The key id on the key line.</param>
    /// <returns>The payload text.</returns>
    private static string ExpectedPayload(string folder, string keyId)
    {
        var lines = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/'))
            .Where(relative => !string.Equals(relative, PluginSignatureFile.FileName, StringComparison.Ordinal))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .Select(relative => $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, relative)))).ToLowerInvariant()} {relative}\n");

        return $"{GoldenHeader}\nkey {keyId}\n{string.Concat(lines)}";
    }
}
