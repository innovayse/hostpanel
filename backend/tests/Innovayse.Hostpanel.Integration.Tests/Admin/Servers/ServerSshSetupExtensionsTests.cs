using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <see cref="ServerSshSetupExtensions.ToSetupDto"/> over the real bundles: a profile a bundle can be served for is offered with
/// the hash and the download path of that very bundle, and a profile that has none (its plugin is gone, or its files were
/// refused at load) is not offered, so the setup never points at a download that answers 404.
/// </summary>
public sealed class ServerSshSetupExtensionsTests
{
    /// <summary>A server with no stored SSH settings yet, which the setup handles as a null row.</summary>
    private static readonly ServerSshSettings? NoSettings = null;

    /// <summary>A profile a plugin ships is offered, with the hash of its own bundle and a download path naming it.</summary>
    [Fact]
    public void AShippedProfile_IsOfferedWithItsOwnBundle()
    {
        var bundles = SshAgentTestSeeds.Bundles(SshAgentTestSeeds.Plugin("panel-plugin"));

        var setup = NoSettings
            .ToSetupDto(5, SshAgentTestSeeds.ProfileId, bundles, new SshAgentOptions());

        setup.Profile.Should().Be(SshAgentTestSeeds.ProfileId);
        setup.BundleSha256.Should().Be(bundles.For(SshAgentTestSeeds.ProfileId)!.Sha256).And.NotBe(bundles.BuiltIn.Sha256);
        setup.BundlePath.Should().Be($"{ServerSshSetupExtensions.BundlePath}?profile={SshAgentTestSeeds.ProfileId}");
    }

    /// <summary>A profile no bundle can be served for is not offered, and the setup carries the built in bundle.</summary>
    [Fact]
    public void AProfileWithoutABundle_IsNotOffered()
    {
        var bundles = SshAgentTestSeeds.Bundles();

        var setup = NoSettings
            .ToSetupDto(5, "gone-plugin-profile", bundles, new SshAgentOptions());

        setup.Profile.Should().BeNull();
        setup.BundleSha256.Should().Be(bundles.BuiltIn.Sha256);
        setup.BundlePath.Should().Be(ServerSshSetupExtensions.BundlePath);
    }

    /// <summary>A module with no profile at all is not offered one either.</summary>
    [Fact]
    public void ANullProfile_IsNotOffered()
    {
        var bundles = SshAgentTestSeeds.Bundles();

        var setup = NoSettings.ToSetupDto(5, null, bundles, new SshAgentOptions());

        setup.Profile.Should().BeNull();
        setup.BundleSha256.Should().Be(bundles.BuiltIn.Sha256);
    }
}
