using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PanelServerConnectionExtensions"/>: the one mapping of a host server connection to the SDK's. Every field is
/// carried over, <c>Port</c> and <c>AccountId</c> are null because the host stores neither, and the printed forms mask the
/// secrets. No container or database.
/// </summary>
public sealed class PanelServerConnectionExtensionsTests
{
    /// <summary>A server access with every member filled.</summary>
    private static readonly PluginServerAccess Access = new(
        new ServerConnectionDto(7, "node-1", ServerModules.Cwp7, "panel.example.test", "203.0.113.9", "the-hash", "root", "the-token", UseSsl: true),
        Password: "the-password");

    /// <summary>Every member is carried over, and the two the host does not store are null.</summary>
    [Fact]
    public void ToPanel_CarriesEveryMemberAndLeavesPortAndAccountIdNull()
    {
        var panel = Access.ToPanel();

        panel.ServerId.Should().Be(7);
        panel.Name.Should().Be("node-1");
        panel.Hostname.Should().Be("panel.example.test");
        panel.IpAddress.Should().Be("203.0.113.9");
        panel.UseSsl.Should().BeTrue();
        panel.Username.Should().Be("root");
        panel.AccessHash.Should().Be("the-hash");
        panel.ApiToken.Should().Be("the-token");
        panel.Password.Should().Be("the-password");
        panel.Port.Should().BeNull();
        panel.AccountId.Should().BeNull();
    }

    /// <summary>A service connection becomes the server plus the service with its provisioning reference.</summary>
    [Fact]
    public void ToPanel_OfAConnection_PairsTheServerWithTheService()
    {
        var connection = new GrantedPluginServiceConnection(11, "acct-1", Access);

        var context = connection.ToPanel();

        context.Server.ServerId.Should().Be(7);
        context.Service.ServiceId.Should().Be(11);
        context.Service.ProvisioningRef.Should().Be("acct-1");
    }

    /// <summary>No printed form of the access, the connection or the mapped result shows a secret.</summary>
    [Fact]
    public void PrintedForms_MaskEverySecret()
    {
        var connection = new GrantedPluginServiceConnection(11, "acct-1", Access);

        var printed = new[] { Access.ToString(), connection.ToString(), Access.ToPanel().ToString(), connection.ToPanel().ToString() };

        foreach (var secret in new[] { "the-hash", "the-token", "the-password" })
        {
            printed.Should().NotContain(text => text.Contains(secret));
        }
    }
}
