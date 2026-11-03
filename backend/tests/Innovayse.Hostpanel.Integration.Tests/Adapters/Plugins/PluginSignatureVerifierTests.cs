using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Hostpanel.Integration.Tests.Fixtures.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginSignatureVerifier"/>: a package is signed when <c>plugin.sig</c> names a trusted key and the
/// Ed25519 signature matches the package's files; a changed, added or removed file, an unknown key, a missing or
/// unreadable signature each fail with their own rule. The host's policy on top: a Production host refuses an
/// uploaded package that fails, unless the admin allows unsigned plugins; Development does not check; a plugin
/// shipped inside the host is never checked. No container or database.
/// </summary>
public sealed class PluginSignatureVerifierTests
{
    /// <summary>A valid signature by a trusted key passes, with and without the enforcing policy.</summary>
    [Fact]
    public void Inspect_ValidSignature_Passes()
    {
        var folder = PluginSigningTestSeeds.NewFolder("verify-valid");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "valid-plugin", new Dictionary<string, string> { ["lib/inner.txt"] = "nested" });
            PluginSigningTestSeeds.Sign(folder);

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull();
            PluginSigningTestSeeds.Verifier(enforced: true)
                .Check(folder, "valid-plugin", PluginOrigin.Uploaded, allowUnsigned: false, new RecordingLoggerProvider().CreateLogger("t"))
                .Should().BeNull();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A file changed after signing makes the signature invalid.</summary>
    [Fact]
    public void Inspect_ChangedFile_IsInvalid()
    {
        var folder = SignedFolder("verify-changed");
        try
        {
            File.WriteAllText(Path.Combine(folder, "readme.txt"), "not what was signed");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A file added after signing makes the signature invalid.</summary>
    [Fact]
    public void Inspect_AddedFile_IsInvalid()
    {
        var folder = SignedFolder("verify-added");
        try
        {
            File.WriteAllText(Path.Combine(folder, "extra.dll"), "smuggled");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A file removed after signing makes the signature invalid.</summary>
    [Fact]
    public void Inspect_RemovedFile_IsInvalid()
    {
        var folder = SignedFolder("verify-removed");
        try
        {
            File.Delete(Path.Combine(folder, "readme.txt"));

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A package signed by a key the host does not trust fails as an unknown key.</summary>
    [Fact]
    public void Inspect_UnknownKey_IsReportedAsUnknownKey()
    {
        var folder = PluginSigningTestSeeds.NewFolder("verify-unknown");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "unknown-key-plugin");
            PluginSigningTestSeeds.Sign(folder, PluginSigningTestSeeds.UntrustedKeyId, PluginSigningTestSeeds.UntrustedKey.PrivateKey);

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.UnknownKey);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A signature made with an untrusted private key but labelled with a trusted key id is invalid, not accepted.</summary>
    [Fact]
    public void Inspect_SignatureOfAnotherKeyUnderATrustedKeyId_IsInvalid()
    {
        var folder = PluginSigningTestSeeds.NewFolder("verify-forged");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "forged-plugin");
            PluginSigningTestSeeds.Sign(folder, PluginSigningTestSeeds.TrustedKeyId, PluginSigningTestSeeds.UntrustedKey.PrivateKey);

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The key id is part of what is signed: a valid signature relabelled with another trusted key id is invalid.</summary>
    [Fact]
    public void Inspect_SignatureRelabelledWithAnotherTrustedKeyId_IsInvalid()
    {
        var folder = SignedFolder("verify-relabel");
        try
        {
            var other = new TrustedPluginKey
            {
                KeyId = "second-key",
                PublicKey = Convert.ToBase64String(PluginSigningTestSeeds.UntrustedKey.PublicKey),
            };
            var verifier = new PluginSignatureVerifier([PluginSigningTestSeeds.TrustedEntry, other], enforced: true);
            var sig = PluginSignatureFile.Parse(File.ReadAllText(Path.Combine(folder, PluginSignatureFile.FileName)));
            File.WriteAllText(
                Path.Combine(folder, PluginSignatureFile.FileName), (sig with { KeyId = "second-key" }).ToJson());

            verifier.Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A package with no <c>plugin.sig</c> is unsigned.</summary>
    [Fact]
    public void Inspect_NoSignatureFile_IsUnsigned()
    {
        var folder = PluginSigningTestSeeds.NewFolder("verify-unsigned");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "unsigned-plugin");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Unsigned);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A <c>plugin.sig</c> that is not a signature file is malformed, never a crash.</summary>
    /// <param name="content">The text written to <c>plugin.sig</c>.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"keyId\":1,\"signature\":\"AAAA\"}")]
    [InlineData("{\"keyId\":\"Bad Key\",\"signature\":\"AAAA\"}")]
    [InlineData("{\"keyId\":\"test-key\",\"signature\":\"not base64!\"}")]
    [InlineData("{\"keyId\":\"test-key\",\"signature\":\"AAAA\"}")]
    public void Inspect_UnreadableSignatureFile_IsMalformed(string content)
    {
        var folder = PluginSigningTestSeeds.NewFolder("verify-malformed");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "malformed-plugin");
            File.WriteAllText(Path.Combine(folder, PluginSignatureFile.FileName), content);

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Malformed);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A <c>plugin.sig</c> over the size cap is refused as malformed without being parsed.</summary>
    [Fact]
    public void Inspect_OversizedSignatureFile_IsMalformed()
    {
        var folder = PluginSigningTestSeeds.NewFolder("verify-oversized");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "oversized-plugin");
            File.WriteAllText(Path.Combine(folder, PluginSignatureFile.FileName), new string(' ', PluginSignatureFile.MaxFileBytes + 1));

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Malformed);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A file name with a line feed would let one file stand for two payload lines, so a package holding one cannot be
    /// signed or verified: it is malformed.
    /// </summary>
    [Fact]
    public void Inspect_FileNameWithALineFeed_IsMalformed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var folder = SignedFolder("verify-newline");
        try
        {
            File.WriteAllText(Path.Combine(folder, "a\nb.txt"), "x");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Malformed);
            var sign = () => PluginSigningTestSeeds.Sign(folder);
            sign.Should().Throw<InvalidDataException>();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// The canonical payload is pinned byte for byte: a header, the key id, then one line per file, the lower-case
    /// hex SHA-256, a space and the forward-slash relative path, sorted ordinally (upper case before lower case),
    /// with the root <c>plugin.sig</c> left out and a nested one kept.
    /// </summary>
    [Fact]
    public void Payload_HasTheDocumentedCanonicalForm()
    {
        var folder = PluginSigningTestSeeds.NewFolder("payload-form");
        try
        {
            File.WriteAllText(Path.Combine(folder, "b.txt"), "b");
            File.WriteAllText(Path.Combine(folder, "B.txt"), "B");
            File.WriteAllText(Path.Combine(folder, PluginSignatureFile.FileName), "ignored");
            Directory.CreateDirectory(Path.Combine(folder, "sub"));
            File.WriteAllText(Path.Combine(folder, "sub", "c.txt"), "c");
            File.WriteAllText(Path.Combine(folder, "sub", PluginSignatureFile.FileName), "nested kept");

            var payload = Encoding.UTF8.GetString(PluginSignaturePayload.Build(folder, "key-1"));

            payload.Should().Be(
                "innovayse-plugin-signature-v1\n"
                + "key key-1\n"
                + "df7e70e5021544f4834bbee64a9e3789febc4be81470df629cad6ddb03320a5c B.txt\n"
                + "3e23e8160039594a33894f6564e1b1348bbd7a0088d42c4acb73eeaed59c009d b.txt\n"
                + "2e7d2c03a9507ae265ecf5b5356885a53393a2029d241394997265a1a25aefc6 sub/c.txt\n"
                + "8dc43cf477e8376c21b4d31a48ce87bbefa310c4f0a13999f5d1b606c0483d57 sub/plugin.sig\n");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A Production host refuses an uploaded package that is unsigned, tampered or signed by an unknown key.</summary>
    /// <param name="scenario">Which kind of bad package to build.</param>
    /// <param name="rule">The rule the host refuses it for.</param>
    [Theory]
    [InlineData(PluginPackageFlaw.Unsigned, PluginSignatureRule.Unsigned)]
    [InlineData(PluginPackageFlaw.Tampered, PluginSignatureRule.Invalid)]
    [InlineData(PluginPackageFlaw.UnknownKey, PluginSignatureRule.UnknownKey)]
    public void Check_Production_RefusesAnUploadedPackageThatFails(PluginPackageFlaw scenario, PluginSignatureRule rule)
    {
        var folder = BadFolder(scenario);
        try
        {
            var refusal = PluginSigningTestSeeds.Verifier(enforced: true)
                .Check(folder, "bad-plugin", PluginOrigin.Uploaded, allowUnsigned: false, new RecordingLoggerProvider().CreateLogger("t"));

            refusal!.Rule.Should().Be(rule);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>With "allow unsigned plugins" on, a Production host admits the same packages and logs a warning naming the plugin and the rule.</summary>
    /// <param name="scenario">Which kind of bad package to build.</param>
    /// <param name="rule">The rule it fails and the warning names.</param>
    [Theory]
    [InlineData(PluginPackageFlaw.Unsigned, PluginSignatureRule.Unsigned)]
    [InlineData(PluginPackageFlaw.Tampered, PluginSignatureRule.Invalid)]
    [InlineData(PluginPackageFlaw.UnknownKey, PluginSignatureRule.UnknownKey)]
    public void Check_Production_AdmitsAFailingPackageWhenUnsignedIsAllowed(PluginPackageFlaw scenario, PluginSignatureRule rule)
    {
        var folder = BadFolder(scenario);
        try
        {
            var logs = new RecordingLoggerProvider();
            var refusal = PluginSigningTestSeeds.Verifier(enforced: true)
                .Check(folder, "bad-plugin", PluginOrigin.Uploaded, allowUnsigned: true, logs.CreateLogger("t"));

            refusal.Should().BeNull();
            logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
                && e.Message.Contains("bad-plugin")
                && e.Message.Contains(rule.ToString()));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A Development host does not check signatures: an unsigned package is admitted.</summary>
    [Fact]
    public void Check_Development_AdmitsAnUnsignedPackage()
    {
        var folder = BadFolder(PluginPackageFlaw.Unsigned);
        try
        {
            PluginSigningTestSeeds.Verifier(enforced: false)
                .Check(folder, "bad-plugin", PluginOrigin.Uploaded, allowUnsigned: false, new RecordingLoggerProvider().CreateLogger("t"))
                .Should().BeNull();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A plugin shipped inside the host image is part of the host: a Production host admits it with no signature at all,
    /// and with the allow-unsigned setting off.
    /// </summary>
    [Fact]
    public void Check_Production_AdmitsABuiltInPluginWithoutASignature()
    {
        var folder = BadFolder(PluginPackageFlaw.Unsigned);
        try
        {
            PluginSigningTestSeeds.Verifier(enforced: true)
                .Check(folder, "bad-plugin", PluginOrigin.BuiltIn, allowUnsigned: false, new RecordingLoggerProvider().CreateLogger("t"))
                .Should().BeNull();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Signatures are enforced in every environment except Development and Testing.</summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="enforced">Whether the verifier made for it enforces signatures.</param>
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    public void Create_EnforcesSignaturesOutsideDevelopmentAndTesting(string environment, bool enforced)
    {
        var verifier = PluginSignatureVerifier.Create(
            new PluginSigningOptions(), new HostingEnvironment { EnvironmentName = environment });

        verifier.Enforced.Should().Be(enforced);
    }

    /// <summary>The trusted set is the maintainers' keys together with the operator's.</summary>
    [Fact]
    public void Create_TrustsBothTheMaintainerAndTheOperatorKeys()
    {
        var folder = SignedFolder("verify-both-lists");
        try
        {
            var options = new PluginSigningOptions { OperatorKeys = [PluginSigningTestSeeds.TrustedEntry] };
            var operatorOnly = PluginSignatureVerifier.Create(options, new HostingEnvironment { EnvironmentName = Environments.Production });

            operatorOnly.Inspect(folder).Should().BeNull();

            var maintainerOnly = PluginSignatureVerifier.Create(
                new PluginSigningOptions { MaintainerKeys = [PluginSigningTestSeeds.TrustedEntry] },
                new HostingEnvironment { EnvironmentName = Environments.Production });

            maintainerOnly.Inspect(folder).Should().BeNull();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Builds a package folder that is signed by the trusted key and unchanged.</summary>
    /// <param name="label">A short label for the folder name.</param>
    /// <returns>The folder; the caller deletes it.</returns>
    private static string SignedFolder(string label)
    {
        var folder = PluginSigningTestSeeds.NewFolder(label);
        PluginSigningTestSeeds.WritePackage(folder, "signed-plugin");
        PluginSigningTestSeeds.Sign(folder);
        return folder;
    }

    /// <summary>Builds a package folder of one of the failing kinds.</summary>
    /// <param name="scenario">Which kind.</param>
    /// <returns>The folder; the caller deletes it.</returns>
    private static string BadFolder(PluginPackageFlaw scenario)
    {
        var folder = PluginSigningTestSeeds.NewFolder("bad-package");
        PluginSigningTestSeeds.WritePackage(folder, "bad-plugin");
        switch (scenario)
        {
            case PluginPackageFlaw.Tampered:
                PluginSigningTestSeeds.Sign(folder);
                File.WriteAllText(Path.Combine(folder, "readme.txt"), "changed after signing");
                break;
            case PluginPackageFlaw.UnknownKey:
                PluginSigningTestSeeds.Sign(folder, PluginSigningTestSeeds.UntrustedKeyId, PluginSigningTestSeeds.UntrustedKey.PrivateKey);
                break;
            case PluginPackageFlaw.Unsigned:
            default:
                break;
        }

        return folder;
    }
}
