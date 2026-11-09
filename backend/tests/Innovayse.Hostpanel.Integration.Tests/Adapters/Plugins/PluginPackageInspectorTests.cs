using Hostpanel.Sdk.Packaging.Signing;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Registry;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginPackageInspector"/> reads the plugin id, the version and the signing key id from the archive's root
/// files with the parsers the store uses, from a bounded stream, and knows only the operator's keys; anything unreadable
/// or ambiguous reads as unknown, never as an error.
/// </summary>
public sealed class PluginPackageInspectorTests
{
    /// <summary>An archive that says nothing usable.</summary>
    private static readonly PluginPackageHeader Nothing = new(null, null, null);

    /// <summary>An inspector whose operator list holds the test key, and whose maintainer list holds another.</summary>
    private readonly PluginPackageInspector _inspector = new(Options.Create(new PluginSigningOptions
    {
        MaintainerKeys = [new TrustedPluginKey { KeyId = "maintainer-key", PublicKey = Convert.ToBase64String(PluginSigningTestSeeds.UntrustedKey.PublicKey) }],
        OperatorKeys = [PluginSigningTestSeeds.TrustedEntry],
    }));

    /// <summary>Reads the header of an archive built from entries.</summary>
    /// <param name="entries">Entry name to text.</param>
    /// <returns>The header.</returns>
    private async Task<PluginPackageHeader> HeaderAsync(Dictionary<string, string> entries)
    {
        await using var stream = new MemoryStream(PluginsTestSeeds.BuildZip(entries));
        return await _inspector.ReadHeaderAsync(stream, CancellationToken.None);
    }

    /// <summary>A signed package yields its plugin id, version and key id, and the stream stays open.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ASignedPackage_YieldsAllThreeAsync()
    {
        await using var stream = new MemoryStream(PluginSigningTestSeeds.SignedZip("acme-pay"));

        var header = await _inspector.ReadHeaderAsync(stream, CancellationToken.None);

        header.Should().Be(new PluginPackageHeader("acme-pay", "1.0.0", PluginSigningTestSeeds.TrustedKeyId));
        stream.CanRead.Should().BeTrue();
    }

    /// <summary>An unsigned package has an id and a version and no key.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnsignedPackage_HasNoKeyAsync()
    {
        await using var stream = new MemoryStream(PluginsTestSeeds.BuildValidPluginZip("acme-pay"));

        var header = await _inspector.ReadHeaderAsync(stream, CancellationToken.None);

        header.Should().Be(new PluginPackageHeader("acme-pay", "1.0.0", null));
    }

    /// <summary>Bytes that are not an archive, and files that are not JSON, read as unknown.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Garbage_ReadsAsUnknownAsync()
    {
        await using var notZip = new MemoryStream("not a zip"u8.ToArray());

        (await _inspector.ReadHeaderAsync(notZip, CancellationToken.None)).Should().Be(Nothing);
        (await HeaderAsync(new() { ["plugin.json"] = "{ nope", ["plugin.sig"] = "[1]" })).Should().Be(Nothing);
    }

    /// <summary>A file longer than its cap reads as missing, however the archive declares its length: the read itself is bounded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnOversizedFile_ReadsAsMissingAsync()
    {
        var padding = new string(' ', PluginPackageInspector.MaxManifestBytes);
        var bigManifest = PluginsTestSeeds.ManifestJson("acme-pay") + padding;
        var bigSignature = "{\"keyId\":\"test-key\",\"signature\":\"" + new string('A', PluginSignatureFile.MaxFileBytes) + "\"}";

        (await HeaderAsync(new() { ["plugin.json"] = bigManifest })).Should().Be(Nothing);
        (await HeaderAsync(new() { ["plugin.json"] = PluginsTestSeeds.ManifestJson("acme-pay"), ["plugin.sig"] = bigSignature }))
            .Should().Be(new PluginPackageHeader("acme-pay", "1.0.0", null));
    }

    /// <summary>
    /// A manifest with the id written twice, or in another case, gives the id the manifest reader gives, because it is the
    /// same reader.
    /// </summary>
    /// <param name="manifest">The manifest text.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("""{"id":"first-one","id":"second-one","name":"n","version":"1.0.0","author":"a","description":"d","type":"Provisioning","category":"c","entryPoint":"E","sdkVersion":"1.0"}""")]
    [InlineData("""{"ID":"upper-id","name":"n","version":"1.0.0","author":"a","description":"d","type":"Provisioning","category":"c","entryPoint":"E","sdkVersion":"1.0"}""")]
    [InlineData("""{"id":"lower-id","Id":"mixed-id","name":"n","version":"1.0.0","author":"a","description":"d","type":"Provisioning","category":"c","entryPoint":"E","sdkVersion":"1.0"}""")]
    public async Task TheManifestIdIsTheOneTheManifestReaderSeesAsync(string manifest)
    {
        var expected = PluginManifestReader.Deserialize(manifest)!.Id;

        var header = await HeaderAsync(new() { ["plugin.json"] = manifest });

        header.PluginId.Should().Be(expected);
    }

    /// <summary>The key id is the one the shared signature-file parser gives; a differently-cased member name is not a key id.</summary>
    /// <param name="signature">The signature file text.</param>
    /// <param name="expectKey">Whether a key id is expected.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("""{"keyId":"test-key","signature":"SIG"}""", true)]
    [InlineData("""{"keyId":"other-key","keyId":"test-key","signature":"SIG"}""", true)]
    [InlineData("""{"KEYID":"test-key","signature":"SIG"}""", false)]
    [InlineData("""{"keyId":"Bad Key","signature":"SIG"}""", false)]
    [InlineData("""{"keyId":"test-key","signature":"short"}""", false)]
    public async Task TheKeyIdIsTheOneTheSignatureParserSeesAsync(string signature, bool expectKey)
    {
        var text = signature.Replace("SIG", Convert.ToBase64String(new byte[PluginSignatureFile.SignatureLength]));
        string? expected = null;
        try
        {
            expected = PluginSignatureFile.Parse(text).KeyId;
        }
        catch (FormatException)
        {
            // Not a signature file: the inspector must say the same.
        }

        var header = await HeaderAsync(new() { ["plugin.json"] = PluginsTestSeeds.ManifestJson("acme-pay"), ["plugin.sig"] = text });

        header.KeyId.Should().Be(expected);
        (header.KeyId is not null).Should().Be(expectKey);
    }

    /// <summary>Two entries whose names differ only in case make the archive ambiguous, and it reads as unknown.</summary>
    /// <param name="extraName">The second, differently-cased entry name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("PLUGIN.JSON")]
    [InlineData("Plugin.Sig")]
    public async Task EntriesDifferingOnlyInCase_ReadAsUnknownAsync(string extraName)
    {
        var entries = new Dictionary<string, string>
        {
            ["plugin.json"] = PluginsTestSeeds.ManifestJson("acme-pay"),
            ["plugin.sig"] = "{}",
            [extraName] = PluginsTestSeeds.ManifestJson("evil-one"),
        };

        (await HeaderAsync(entries)).Should().Be(Nothing);
    }

    /// <summary>A file whose name differs in case from the one the store reads is not read at all.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AWrongCaseFileName_IsNotReadAsync()
    {
        (await HeaderAsync(new() { ["Plugin.json"] = PluginsTestSeeds.ManifestJson("acme-pay") })).Should().Be(Nothing);
    }

    /// <summary>Only a key in the operator's list counts; the maintainers' key does not.</summary>
    [Fact]
    public void OnlyOperatorKeys_Count()
    {
        _inspector.IsOperatorKey(PluginSigningTestSeeds.TrustedKeyId).Should().BeTrue();
        _inspector.IsOperatorKey("maintainer-key").Should().BeFalse();
        _inspector.IsOperatorKey("unknown").Should().BeFalse();
    }
}
