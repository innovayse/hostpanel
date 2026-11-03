using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary><see cref="PluginApiOptions"/>: the defaults, the store quota and which values the host refuses at startup.</summary>
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

    /// <summary>The default store character cap is the documented constant, valid, and the store quota carries both caps.</summary>
    [Fact]
    public void Defaults_AllowTheDefaultStoreCharCap()
    {
        var options = new PluginApiOptions();

        options.MaxStoreCharsPerScope.Should().Be(PluginApiOptions.DefaultMaxStoreCharsPerScope);
        options.IsValid.Should().BeTrue();
        options.StoreQuota.Should().Be(
            new PluginStoreQuota(PluginApiOptions.DefaultMaxStoreKeysPerScope, PluginApiOptions.DefaultMaxStoreCharsPerScope));
    }

    /// <summary>
    /// A store character cap from the floor (one full key and value) to the ceiling is valid; one below the floor, zero
    /// and one over the ceiling are not.
    /// </summary>
    /// <param name="chars">A configured store character cap.</param>
    /// <param name="valid">Whether the host accepts it.</param>
    [Theory]
    [InlineData(0L, false)]
    [InlineData(PluginApiOptions.MaxStoreCharsPerScopeFloor - 1, false)]
    [InlineData(PluginApiOptions.MaxStoreCharsPerScopeFloor, true)]
    [InlineData(PluginApiOptions.MaxStoreCharsPerScopeCeiling, true)]
    [InlineData(PluginApiOptions.MaxStoreCharsPerScopeCeiling + 1, false)]
    public void IsValid_AcceptsOnlyABoundedStoreCharCap(long chars, bool valid)
    {
        new PluginApiOptions { MaxStoreCharsPerScope = chars }.IsValid.Should().Be(valid);
    }

    /// <summary>The floor fits exactly one key of the longest length with one value of the longest length.</summary>
    [Fact]
    public void StoreCharFloor_FitsOneFullWrite()
    {
        PluginApiOptions.MaxStoreCharsPerScopeFloor.Should().Be(
            PluginDataEntry.CharsOf(
                new string('a', PluginDataEntry.MaxKeyLength), new string('x', PluginDataEntry.MaxValueLength)));
    }

    /// <summary>The store quota follows the configured caps.</summary>
    [Fact]
    public void StoreQuota_FollowsTheConfiguredCaps()
    {
        var options = new PluginApiOptions { MaxStoreKeysPerScope = 7, MaxStoreCharsPerScope = 70_000 };

        options.StoreQuota.Should().Be(new PluginStoreQuota(7, 70_000));
    }
}
