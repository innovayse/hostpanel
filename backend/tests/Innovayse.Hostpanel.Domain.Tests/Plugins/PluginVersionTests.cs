using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Plugins.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Plugins;

/// <summary>Tests for <see cref="PluginVersion"/>: strict parsing and semantic version ordering.</summary>
public sealed class PluginVersionTests
{
    /// <summary>The older version compares below the newer one.</summary>
    /// <param name="older">The older version text.</param>
    /// <param name="newer">The newer version text.</param>
    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.9.9", "2.0.0")]
    [InlineData("2.0.0-beta.2", "2.0.0-beta.10")]
    [InlineData("2.0.0-alpha", "2.0.0-beta")]
    [InlineData("2.0.0-rc.1", "2.0.0")]
    [InlineData("2.0.0-1", "2.0.0-alpha")]
    [InlineData("2.0.0-99999999999999999999", "2.0.0-100000000000000000000")]
    public void CompareTo_OrdersBySemanticPrecedence(string older, string newer)
    {
        PluginVersion.TryParse(older, out var low).Should().BeTrue();
        PluginVersion.TryParse(newer, out var high).Should().BeTrue();

        low.CompareTo(high).Should().BeNegative();
        high.CompareTo(low).Should().BePositive();
    }

    /// <summary>Build metadata does not change precedence.</summary>
    [Fact]
    public void CompareTo_IgnoresBuildMetadata()
    {
        PluginVersion.TryParse("1.2.0+5", out var withBuild).Should().BeTrue();
        PluginVersion.TryParse("1.2.0", out var plain).Should().BeTrue();

        withBuild.CompareTo(plain).Should().Be(0);
    }

    /// <summary>Text that is not a strict version is not parsed.</summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.x.0")]
    [InlineData("1.2.3.4")]
    [InlineData("1.0.0-")]
    [InlineData("-1.0.0")]
    [InlineData("1.0")]
    [InlineData("1")]
    [InlineData("01.0.0")]
    [InlineData("1.00.0")]
    [InlineData("1.0.007")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+a..b")]
    [InlineData("1.0.0+a+b")]
    [InlineData("1.0.0-a_b")]
    [InlineData(" 1.0.0")]
    [InlineData("1.0.0 ")]
    [InlineData("+1.0.0")]
    [InlineData("99999999999.0.0")]
    [InlineData("1.0.2147483648")]
    public void TryParse_RefusesNonVersions(string? text)
    {
        PluginVersion.TryParse(text, out _).Should().BeFalse();
    }

    /// <summary>The largest number an int holds and a zero part are versions.</summary>
    [Fact]
    public void TryParse_AcceptsZeroAndTheLargestPart()
    {
        PluginVersion.TryParse("0.0.0", out _).Should().BeTrue();
        PluginVersion.TryParse("1.0.2147483647", out var version).Should().BeTrue();
        version.Patch.Should().Be(int.MaxValue);
    }

    /// <summary>The short form of a version is not the same version as the full one: it is not a version at all.</summary>
    [Fact]
    public void Compare_ShortFormIsNotEquivalentToTheFullForm()
    {
        PluginVersion.Compare("1.0.0", "1.0").Should().Be(PluginVersionChange.Incomparable);
        PluginVersion.Compare("1.0", "1.0.0").Should().Be(PluginVersionChange.Incomparable);
    }

    /// <summary>Compare names the relation of the upload to the installed version, and the same version across build metadata.</summary>
    /// <param name="installed">The installed version text.</param>
    /// <param name="uploaded">The uploaded version text.</param>
    /// <param name="expected">The relation.</param>
    [Theory]
    [InlineData("1.0.0", "1.0.1", PluginVersionChange.Newer)]
    [InlineData("1.0.1", "1.0.0", PluginVersionChange.Older)]
    [InlineData("1.0.0", "1.0.0+7", PluginVersionChange.Same)]
    [InlineData("1.0.0-rc.1", "1.0.0", PluginVersionChange.Newer)]
    [InlineData("1.0.0", "garbage", PluginVersionChange.Incomparable)]
    [InlineData("garbage", "1.0.0", PluginVersionChange.Incomparable)]
    [InlineData(null, "1.0.0", PluginVersionChange.Incomparable)]
    public void Compare_NamesTheRelation(string? installed, string? uploaded, PluginVersionChange expected)
    {
        PluginVersion.Compare(installed, uploaded).Should().Be(expected);
    }
}
