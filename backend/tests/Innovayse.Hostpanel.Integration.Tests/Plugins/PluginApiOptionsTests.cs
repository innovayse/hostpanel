using Innovayse.Hostpanel.Application.Common.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary><see cref="PluginApiOptions"/>: the limit given to the web server and what counts as a valid cap.</summary>
public sealed class PluginApiOptionsTests
{
    /// <summary>At the 64 MiB ceiling the server limit is the cap, one read chunk and 1/64 of the cap, computed without overflow.</summary>
    [Fact]
    public void TheServerBodyLimit_AtTheCeiling_IsTheCapPlusTheAllowance()
    {
        var options = new PluginApiOptions { MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling };

        options.ServerBodyLimit.Should().Be(67_108_864L + 8_192L + 1_048_576L);
        options.ServerBodyLimit.Should().BeGreaterThan(options.MaxBodyBytes);
    }

    /// <summary>With a cap of one byte the limit is still above the cap by one read chunk.</summary>
    [Fact]
    public void TheServerBodyLimit_ForTheSmallestCap_IsOneChunkAboveIt()
    {
        new PluginApiOptions { MaxBodyBytes = 1 }.ServerBodyLimit.Should().Be(1L + PluginApiOptions.ReadChunkBytes);
    }

    /// <summary>The heartbeat defaults to 15 seconds, and the span is the seconds.</summary>
    [Fact]
    public void TheHeartbeat_DefaultsToFifteenSeconds()
    {
        var options = new PluginApiOptions();

        options.StreamHeartbeatSeconds.Should().Be(15);
        options.StreamHeartbeat.Should().Be(TimeSpan.FromSeconds(15));
        options.IsValid.Should().BeTrue();
    }

    /// <summary>A heartbeat of zero, or longer than the ceiling, is not valid; the bounds themselves are.</summary>
    /// <param name="seconds">The configured heartbeat.</param>
    /// <param name="valid">Whether the options are valid.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(PluginApiOptions.StreamHeartbeatSecondsCeiling, true)]
    [InlineData(PluginApiOptions.StreamHeartbeatSecondsCeiling + 1, false)]
    public void TheHeartbeat_IsValidFromOneSecondToTheCeiling(int seconds, bool valid)
    {
        new PluginApiOptions { StreamHeartbeatSeconds = seconds }.IsValid.Should().Be(valid);
    }

    /// <summary>The ceiling is valid and one byte past it is not.</summary>
    [Fact]
    public void TheCeiling_IsValid_AndOneMoreIsNot()
    {
        new PluginApiOptions { MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling }.IsValid.Should().BeTrue();
        new PluginApiOptions { MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling + 1 }.IsValid.Should().BeFalse();
    }

    /// <summary>The event cap defaults to 64 KiB and the options are valid with it.</summary>
    [Fact]
    public void TheEventCap_DefaultsTo64KiB()
    {
        var options = new PluginApiOptions();

        options.StreamEventMaxBytes.Should().Be(65_536);
        options.IsValid.Should().BeTrue();
    }

    /// <summary>An event cap of zero, or over the ceiling, is not valid; the bounds themselves are.</summary>
    /// <param name="bytes">The configured cap.</param>
    /// <param name="valid">Whether the options are valid.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(PluginApiOptions.StreamEventMaxBytesCeiling, true)]
    [InlineData(PluginApiOptions.StreamEventMaxBytesCeiling + 1, false)]
    public void TheEventCap_IsValidFromOneByteToTheCeiling(int bytes, bool valid)
    {
        new PluginApiOptions { StreamEventMaxBytes = bytes }.IsValid.Should().Be(valid);
    }
}
