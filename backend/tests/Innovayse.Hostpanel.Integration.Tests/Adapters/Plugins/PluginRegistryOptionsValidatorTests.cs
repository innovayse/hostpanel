using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistryOptionsValidator"/> stops the start when the registry address is not an https URL, the
/// registry is on without an address, or a limit is out of range; the shipped default (off, no address) is valid.
/// </summary>
public sealed class PluginRegistryOptionsValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly PluginRegistryOptionsValidator _validator = new();

    /// <summary>The default options are valid and carry no address.</summary>
    [Fact]
    public void TheDefaultOptions_AreValidAndHaveNoAddress()
    {
        var options = new PluginRegistryOptions();

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
        options.Enabled.Should().BeFalse();
        options.BaseUrl.Should().BeEmpty();
    }

    /// <summary>A registry that is on needs an address.</summary>
    [Fact]
    public void EnabledWithoutAnAddress_Fails()
    {
        var result = _validator.Validate(null, new PluginRegistryOptions { Enabled = true });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PluginRegistry__BaseUrl");
    }

    /// <summary>An https address is valid, on or off.</summary>
    /// <param name="enabled">Whether the registry is on.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnHttpsAddress_IsValid(bool enabled)
    {
        _validator.Validate(null, new PluginRegistryOptions { Enabled = enabled, BaseUrl = "https://registry.example.com/api/" }).Succeeded.Should().BeTrue();
    }

    /// <summary>An address that is not a plain absolute https URL fails, even while the registry is off.</summary>
    /// <param name="url">The address.</param>
    [Theory]
    [InlineData("http://registry.example.com/")]
    [InlineData("registry.example.com")]
    [InlineData("/relative")]
    [InlineData("ftp://registry.example.com/")]
    [InlineData("https://user:pass@registry.example.com/")]
    [InlineData("https://registry.example.com/?token=1")]
    [InlineData("https://registry.example.com/#x")]
    public void AnUnsafeAddress_Fails(string url)
    {
        _validator.Validate(null, new PluginRegistryOptions { BaseUrl = url }).Failed.Should().BeTrue();
    }

    /// <summary>A time limit outside one second to five minutes fails.</summary>
    /// <param name="seconds">The time limit in seconds.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void ATimeoutOutOfRange_Fails(int seconds)
    {
        _validator.Validate(null, new PluginRegistryOptions { Timeout = TimeSpan.FromSeconds(seconds) }).Failed.Should().BeTrue();
    }

    /// <summary>A download limit of zero or over 100 MiB fails.</summary>
    /// <param name="bytes">The limit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(104_857_601)]
    public void ADownloadLimitOutOfRange_Fails(long bytes)
    {
        _validator.Validate(null, new PluginRegistryOptions { MaxDownloadBytes = bytes }).Failed.Should().BeTrue();
    }
}
