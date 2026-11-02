using Innovayse.Hostpanel.Application.Common.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary><see cref="PluginApiOptions"/>: the default body cap and which values the host refuses at startup.</summary>
public sealed class PluginApiOptionsTests
{
    /// <summary>The default cap is 1 MiB and valid.</summary>
    [Fact]
    public void Defaults_AllowOneMebibyte()
    {
        var options = new PluginApiOptions();

        options.MaxBodyBytes.Should().Be(1_048_576);
        options.IsValid.Should().BeTrue();
    }

    /// <summary>A cap from one byte to the ceiling is valid; zero, a negative one and one over the ceiling are not.</summary>
    /// <param name="bytes">A configured cap.</param>
    /// <param name="valid">Whether the host accepts it.</param>
    [Theory]
    [InlineData(1, true)]
    [InlineData(PluginApiOptions.MaxBodyBytesCeiling, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(PluginApiOptions.MaxBodyBytesCeiling + 1, false)]
    public void IsValid_AcceptsOnlyABoundedCap(int bytes, bool valid)
    {
        new PluginApiOptions { MaxBodyBytes = bytes }.IsValid.Should().Be(valid);
    }

    /// <summary>The default store key cap is the documented constant and valid.</summary>
    [Fact]
    public void Defaults_AllowTheDefaultStoreKeyCap()
    {
        var options = new PluginApiOptions();

        options.MaxStoreKeysPerScope.Should().Be(PluginApiOptions.DefaultMaxStoreKeysPerScope);
        options.IsValid.Should().BeTrue();
    }

    /// <summary>A store key cap from one to the ceiling is valid; zero and one over the ceiling are not.</summary>
    /// <param name="keys">A configured store key cap.</param>
    /// <param name="valid">Whether the host accepts it.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(PluginApiOptions.MaxStoreKeysPerScopeCeiling, true)]
    [InlineData(PluginApiOptions.MaxStoreKeysPerScopeCeiling + 1, false)]
    public void IsValid_AcceptsOnlyABoundedStoreKeyCap(int keys, bool valid)
    {
        new PluginApiOptions { MaxStoreKeysPerScope = keys }.IsValid.Should().Be(valid);
    }
}
