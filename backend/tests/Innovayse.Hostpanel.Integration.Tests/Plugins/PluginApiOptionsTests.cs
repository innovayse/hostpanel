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

    /// <summary>The ceiling is valid and one byte past it is not.</summary>
    [Fact]
    public void TheCeiling_IsValid_AndOneMoreIsNot()
    {
        new PluginApiOptions { MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling }.IsValid.Should().BeTrue();
        new PluginApiOptions { MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling + 1 }.IsValid.Should().BeFalse();
    }
}
