using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.SshAgent;

/// <summary>
/// The SSH key agent bundles: the built in one is deterministic and versioned, and a bundle for a plugin's profile is the same
/// files plus the profile and helpers the plugin ships, packed the same way, with the plugin's file winning over a built in
/// file at the same path.
/// </summary>
public sealed class SshAgentBundlesTests
{
    /// <summary>Two bundles built from the same files are byte-identical and carry the agent version.</summary>
    [Fact]
    public void BuiltIn_IsDeterministicAndVersioned()
    {
        var first = SshAgentTestSeeds.Bundles().BuiltIn;
        var second = SshAgentTestSeeds.Bundles().BuiltIn;

        first.Sha256.Should().Be(second.Sha256).And.MatchRegex("^[0-9a-f]{64}$");
        first.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        Entries(first.Content.ToArray()).Keys.Should().Contain(["hostpanel-ssh-key-agent", "install.sh", "profiles/generic-linux.conf"]);
    }

    /// <summary>The host's default profile is always the built in bundle, which no plugin can replace.</summary>
    [Fact]
    public void For_TheDefaultProfile_IsTheBuiltInBundle()
    {
        var bundles = SshAgentTestSeeds.Bundles(SshAgentTestSeeds.Plugin("panel-plugin"));

        bundles.For("generic-linux").Should().BeSameAs(bundles.BuiltIn);
    }

    /// <summary>An id no loaded plugin ships and the agent does not embed has no bundle.</summary>
    [Fact]
    public void For_AnUnknownProfile_IsNull()
    {
        SshAgentTestSeeds.Bundles(SshAgentTestSeeds.Plugin("panel-plugin")).For("nobody-ships-this").Should().BeNull();
    }

    /// <summary>A plugin's profile is bundled with the built in files, its profile file and its helpers, the helper executable.</summary>
    [Fact]
    public void For_APluginProfile_HoldsTheBuiltInFilesAndThePluginsOwn()
    {
        var bundles = SshAgentTestSeeds.Bundles(SshAgentTestSeeds.Plugin("panel-plugin"));

        var bundle = bundles.For(SshAgentTestSeeds.ProfileId);

        bundle.Should().NotBeNull();
        bundle!.Sha256.Should().NotBe(bundles.BuiltIn.Sha256);
        bundle.Version.Should().Be(bundles.BuiltIn.Version);
        var entries = Entries(bundle.Content.ToArray());
        entries.Keys.Should().Contain(["install.sh", "profiles/generic-linux.conf"]);
        Encoding.UTF8.GetString(entries["profiles/examplepanel.conf"].Bytes).Should().Be("[agent]\nprovider = examplepanel\n");
        entries["profiles/examplepanel.conf"].Mode.Should().NotHaveFlag(UnixFileMode.UserExecute);
        entries[$"providers/examplepanel/{SshAgentTestSeeds.HelperName}"].Mode.Should().HaveFlag(UnixFileMode.UserExecute);
    }

    /// <summary>The same plugin always yields the same bytes, in this registry and in a fresh one, and the bundle is packed once.</summary>
    [Fact]
    public void For_APluginProfile_IsDeterministicAndKept()
    {
        var bundles = SshAgentTestSeeds.Bundles(SshAgentTestSeeds.Plugin("panel-plugin"));

        var first = bundles.For(SshAgentTestSeeds.ProfileId)!;
        var again = bundles.For(SshAgentTestSeeds.ProfileId)!;
        var fresh = SshAgentTestSeeds.Bundles(SshAgentTestSeeds.Plugin("panel-plugin")).For(SshAgentTestSeeds.ProfileId)!;

        again.Should().BeSameAs(first);
        fresh.Sha256.Should().Be(first.Sha256);
    }

    /// <summary>A plugin that ships the id of a built in profile replaces the built in profile file: the plugin's bytes win, once.</summary>
    [Fact]
    public void For_APluginProfileWithABuiltInId_ReplacesTheBuiltInFile()
    {
        var bundles = SshAgentTestSeeds.Bundles(SshAgentTestSeeds.Plugin("panel-plugin", "cwp", "[agent]\nprovider = cwp\nmax_keys = 3\n"));

        var entries = Entries(bundles.For("cwp")!.Content.ToArray());

        Encoding.UTF8.GetString(entries["profiles/cwp.conf"].Bytes).Should().Be("[agent]\nprovider = cwp\nmax_keys = 3\n");
        Encoding.UTF8.GetString(Entries(bundles.BuiltIn.Content.ToArray())["profiles/cwp.conf"].Bytes).Should().NotContain("max_keys = 3");
    }

    /// <summary>A profile the agent embeds is the built in bundle while no plugin ships one with that id.</summary>
    [Fact]
    public void For_AnEmbeddedProfileNoPluginShips_IsTheBuiltInBundle()
    {
        var bundles = SshAgentTestSeeds.Bundles();

        bundles.For("cwp").Should().BeSameAs(bundles.BuiltIn);
    }

    /// <summary>Reads every entry of a bundle by its path below the single root folder.</summary>
    /// <param name="content">The <c>.tar.gz</c> bytes.</param>
    /// <returns>The entries by path, each with its bytes and mode.</returns>
    private static Dictionary<string, (byte[] Bytes, UnixFileMode Mode)> Entries(byte[] content)
    {
        var entries = new Dictionary<string, (byte[] Bytes, UnixFileMode Mode)>(StringComparer.Ordinal);
        using var tar = new TarReader(new GZipStream(new MemoryStream(content), CompressionMode.Decompress));
        while (tar.GetNextEntry() is { } entry)
        {
            using var copy = new MemoryStream();
            entry.DataStream!.CopyTo(copy);
            entries.Add(entry.Name.Split('/', 2)[1], (copy.ToArray(), entry.Mode));
        }

        return entries;
    }
}
