using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>Which configured public addresses can be told to the registry: https, a public host, nothing else.</summary>
public sealed class RegistryInstanceAddressTests
{
    /// <summary>A public https address is kept, with a lower-case host and no trailing slash.</summary>
    /// <param name="configured">The configured address.</param>
    /// <param name="expected">The normalised address.</param>
    [Theory]
    [InlineData("https://panel.example.com", "https://panel.example.com")]
    [InlineData("https://Panel.Example.com/", "https://panel.example.com")]
    [InlineData("  https://panel.example.com:8443/hosting/  ", "https://panel.example.com:8443/hosting")]
    public void APublicHttpsAddress_IsKept(string configured, string expected)
    {
        RegistryInstanceAddress.TryNormalize(configured, out var address).Should().BeTrue();
        address.Should().Be(expected);
    }

    /// <summary>An address the registry could not or must not fetch from is refused.</summary>
    /// <param name="configured">The configured address.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://panel.example.com")]
    [InlineData("panel.example.com")]
    [InlineData("https://user:secret@panel.example.com")]
    [InlineData("https://panel.example.com/?x=1")]
    [InlineData("https://panel.example.com/#top")]
    [InlineData("https://localhost")]
    [InlineData("https://panel")]
    [InlineData("https://panel.local")]
    [InlineData("https://panel.internal")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://10.0.0.5")]
    [InlineData("https://192.168.1.10")]
    [InlineData("https://172.20.0.1")]
    [InlineData("https://169.254.169.254")]
    [InlineData("https://[::1]")]
    [InlineData("https://[fd00::1]")]
    public void AnAddressTheRegistryCannotUse_IsRefused(string? configured)
    {
        RegistryInstanceAddress.TryNormalize(configured, out var address).Should().BeFalse();
        address.Should().BeEmpty();
    }

    /// <summary>An address longer than the registry keeps is refused.</summary>
    [Fact]
    public void AnOverlongAddress_IsRefused()
    {
        RegistryInstanceAddress.TryNormalize("https://" + new string('a', 250) + ".example.com", out _).Should().BeFalse();
    }
}
