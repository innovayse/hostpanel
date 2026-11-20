using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Plugins.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Plugins;

/// <summary>The rules an <see cref="InstalledPlugin"/> install record is created and updated under.</summary>
public sealed class InstalledPluginTests
{
    /// <summary>A valid SHA-256 digest.</summary>
    private static readonly string Digest = new('a', InstalledPlugin.Sha256Length);

    /// <summary>The moment of the first install.</summary>
    private static readonly DateTimeOffset InstalledAt = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the facts of an unsigned upload.</summary>
    /// <returns>The facts.</returns>
    private static PluginPackageFacts Unsigned()
    {
        return new PluginPackageFacts(PluginSource.Uploaded, PluginSignerKind.Unsigned, null, 2048, Digest);
    }

    /// <summary>A first install keeps what it was created with and has not been updated.</summary>
    [Fact]
    public void Create_KeepsTheInstallFactsAndHasNoUpdate()
    {
        var record = InstalledPlugin.Create("acme-usage", Unsigned(), "user-1", "Ada Admin", InstalledAt);

        record.PluginId.Should().Be("acme-usage");
        record.Source.Should().Be(PluginSource.Uploaded);
        record.Signer.Should().Be(PluginSignerKind.Unsigned);
        record.SignerKeyId.Should().BeNull();
        record.PackageSizeBytes.Should().Be(2048);
        record.PackageSha256.Should().Be(Digest);
        record.InstalledAt.Should().Be(InstalledAt);
        record.UpdatedAt.Should().BeNull();
        record.InstalledByUserId.Should().Be("user-1");
        record.InstalledByName.Should().Be("Ada Admin");
    }

    /// <summary>A blank installer is stored as unknown and an over-long one is cut to the column length.</summary>
    [Fact]
    public void Create_NormalisesTheInstaller()
    {
        var blank = InstalledPlugin.Create("acme-usage", Unsigned(), " ", null, InstalledAt);
        var tooLong = InstalledPlugin.Create("acme-usage", Unsigned(), null, new string('x', InstalledPlugin.UserLength + 10), InstalledAt);

        blank.InstalledByUserId.Should().BeNull();
        blank.InstalledByName.Should().BeNull();
        tooLong.InstalledByName.Should().HaveLength(InstalledPlugin.UserLength);
    }

    /// <summary>A later package replaces the package facts and stamps the update, and the first install is kept.</summary>
    [Fact]
    public void RecordUpload_ReplacesThePackageFactsAndKeepsTheFirstInstall()
    {
        var record = InstalledPlugin.Create("acme-usage", Unsigned(), "user-1", "Ada Admin", InstalledAt);
        var later = InstalledAt.AddDays(3);

        record.RecordUpload(
            new PluginPackageFacts(PluginSource.Registry, PluginSignerKind.OwnKey, "own-key", 4096, new string('b', InstalledPlugin.Sha256Length)),
            later);

        record.Source.Should().Be(PluginSource.Registry);
        record.Signer.Should().Be(PluginSignerKind.OwnKey);
        record.SignerKeyId.Should().Be("own-key");
        record.PackageSizeBytes.Should().Be(4096);
        record.UpdatedAt.Should().Be(later);
        record.InstalledAt.Should().Be(InstalledAt);
        record.InstalledByName.Should().Be("Ada Admin");
    }

    /// <summary>An empty plugin id is refused.</summary>
    /// <param name="pluginId">The plugin id.</param>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyPluginId_Throws(string pluginId)
    {
        var create = () => InstalledPlugin.Create(pluginId, Unsigned(), null, null, InstalledAt);

        create.Should().Throw<ArgumentException>();
    }

    /// <summary>A plugin that ships with the build is not an install record.</summary>
    [Fact]
    public void Create_BuiltInSource_Throws()
    {
        var create = () => InstalledPlugin.Create(
            "acme-usage", new PluginPackageFacts(PluginSource.BuiltIn, PluginSignerKind.Unsigned, null, 1, Digest), null, null, InstalledAt);

        create.Should().Throw<ArgumentException>();
    }

    /// <summary>A signed package names its key and an unsigned one names none.</summary>
    /// <param name="signer">Who signed.</param>
    /// <param name="keyId">The key id.</param>
    [Theory]
    [InlineData(PluginSignerKind.Unsigned, "some-key")]
    [InlineData(PluginSignerKind.VerifiedPublisher, null)]
    [InlineData(PluginSignerKind.OwnKey, " ")]
    public void Create_SignerAndKeyDisagree_Throws(PluginSignerKind signer, string? keyId)
    {
        var create = () => InstalledPlugin.Create(
            "acme-usage", new PluginPackageFacts(PluginSource.Uploaded, signer, keyId, 1, Digest), null, null, InstalledAt);

        create.Should().Throw<ArgumentException>();
    }

    /// <summary>A signing key id longer than its column is refused.</summary>
    [Fact]
    public void Create_SignerKeyIdTooLong_Throws()
    {
        var create = () => InstalledPlugin.Create(
            "acme-usage",
            new PluginPackageFacts(PluginSource.Uploaded, PluginSignerKind.OwnKey, new string('k', InstalledPlugin.SignerKeyIdLength + 1), 1, Digest),
            null,
            null,
            InstalledAt);

        create.Should().Throw<ArgumentException>().WithMessage("*longer than*");
    }

    /// <summary>A package that is empty, or whose digest is not a SHA-256, is refused, on a first install and on an update alike.</summary>
    [Fact]
    public void BadPackageFacts_Throw()
    {
        var record = InstalledPlugin.Create("acme-usage", Unsigned(), null, null, InstalledAt);

        var empty = () => record.RecordUpload(new PluginPackageFacts(PluginSource.Uploaded, PluginSignerKind.Unsigned, null, 0, Digest), InstalledAt);
        var digest = () => InstalledPlugin.Create(
            "acme-usage", new PluginPackageFacts(PluginSource.Uploaded, PluginSignerKind.Unsigned, null, 1, "short"), null, null, InstalledAt);

        empty.Should().Throw<ArgumentOutOfRangeException>();
        digest.Should().Throw<ArgumentException>();
    }

    /// <summary>A digest of the right length that is not lower-case hex (upper case, or other characters) is refused.</summary>
    /// <param name="digest">The digest to refuse.</param>
    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Create_DigestNotLowerCaseHex_Throws(string digest)
    {
        var create = () => InstalledPlugin.Create(
            "acme-usage", new PluginPackageFacts(PluginSource.Uploaded, PluginSignerKind.Unsigned, null, 1, digest), null, null, InstalledAt);

        create.Should().Throw<ArgumentException>();
    }
}
