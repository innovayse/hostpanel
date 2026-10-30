using Innovayse.Hostpanel.Infrastructure.Monitoring;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.GitHub;

/// <summary>Unit tests for <see cref="InstalledVersionProvider.ParseVersion"/>.</summary>
public sealed class InstalledVersionProviderTests
{
    /// <summary>A standard version string is returned as-is.</summary>
    [Fact]
    public void ParseVersion_PlainVersion_ReturnsIt()
    {
        InstalledVersionProvider.ParseVersion("1.4.0").Should().Be("1.4.0");
    }

    /// <summary>The <c>+&lt;commit&gt;</c> metadata suffix is stripped.</summary>
    [Fact]
    public void ParseVersion_VersionWithShaSuffix_StripsTheSuffix()
    {
        InstalledVersionProvider.ParseVersion("1.4.0+abc1234").Should().Be("1.4.0");
    }

    /// <summary>A version with a long commit hash suffix is stripped correctly.</summary>
    [Fact]
    public void ParseVersion_VersionWithLongShaSuffix_StripsTheSuffix()
    {
        InstalledVersionProvider.ParseVersion("2.1.3+deadbeefdeadbeef").Should().Be("2.1.3");
    }

    /// <summary>A <see langword="null"/> input returns the dev fallback.</summary>
    [Fact]
    public void ParseVersion_Null_ReturnsDev()
    {
        InstalledVersionProvider.ParseVersion(null).Should().Be("dev");
    }

    /// <summary>An empty string returns the dev fallback.</summary>
    [Fact]
    public void ParseVersion_Empty_ReturnsDev()
    {
        InstalledVersionProvider.ParseVersion(string.Empty).Should().Be("dev");
    }

    /// <summary>A whitespace-only string returns the dev fallback.</summary>
    [Fact]
    public void ParseVersion_Whitespace_ReturnsDev()
    {
        InstalledVersionProvider.ParseVersion("   ").Should().Be("dev");
    }
}
