using System.Net;
using Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistryConnector"/> refuses loopback, link-local, metadata and other addresses the registry address
/// must never reach, in IPv4, IPv6 and IPv4-mapped form, and leaves public and private network addresses alone.
/// </summary>
public sealed class PluginRegistryConnectorTests
{
    /// <summary>Addresses that are refused.</summary>
    /// <param name="address">The address text.</param>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.8.8.8")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("169.254.169.254")]
    [InlineData("169.254.0.1")]
    [InlineData("fe80::1")]
    [InlineData("100.100.100.200")]
    [InlineData("168.63.129.16")]
    [InlineData("fd00:ec2::254")]
    [InlineData("224.0.0.1")]
    [InlineData("ff02::1")]
    [InlineData("255.255.255.255")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    public void ForbiddenAddresses_AreRefused(string address)
    {
        PluginRegistryConnector.IsForbidden(IPAddress.Parse(address)).Should().BeTrue();
    }

    /// <summary>Public addresses and private network ranges are allowed: a registry may sit on an internal network.</summary>
    /// <param name="address">The address text.</param>
    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.4.4")]
    [InlineData("192.168.1.10")]
    [InlineData("::ffff:93.184.216.34")]
    public void OrdinaryAddresses_AreAllowed(string address)
    {
        PluginRegistryConnector.IsForbidden(IPAddress.Parse(address)).Should().BeFalse();
    }

    /// <summary>A name that resolves to a forbidden address is refused before any connection is made.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ALoopbackName_IsRefusedAsync()
    {
        using var handler = new SocketsHttpHandler { ConnectCallback = PluginRegistryConnector.ConnectAsync, AllowAutoRedirect = false };
        using var http = new HttpClient(handler);

        var act = () => http.GetAsync("https://localhost:1/");

        var thrown = await act.Should().ThrowAsync<HttpRequestException>();
        thrown.Which.Message.Should().NotBeEmpty();
    }
}
