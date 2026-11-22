using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <see cref="ServerConnectionExtensions.ToPanelConnection"/> and <see cref="ServerMapper.ToConnection"/>: the connection a panel
/// plugin receives carries the server's address, SSL choice and credentials, no port (the host keeps none) and the stored password
/// only when the caller hands it over, and the connection details every built in provider receives never carry that password.
/// </summary>
public sealed class ServerConnectionExtensionsTests
{
    /// <summary>The mapper copies the address, SSL choice, user and secrets, leaves the port and account id out, and uses the password it is given.</summary>
    [Fact]
    public void ToPanelConnection_MapsTheServerAndTheGivenPassword()
    {
        var dto = new ServerConnectionDto(5, "srv", ServerModules.CPanel, "panel.example.test", "203.0.113.9", "hash-value", "root", "token-value", true);

        var connection = dto.ToPanelConnection("stored-password");

        connection.ServerId.Should().Be(5);
        connection.Name.Should().Be("srv");
        connection.Hostname.Should().Be("panel.example.test");
        connection.IpAddress.Should().Be("203.0.113.9");
        connection.Port.Should().BeNull("the host keeps no panel port");
        connection.UseSsl.Should().BeTrue();
        connection.Username.Should().Be("root");
        connection.AccessHash.Should().Be("hash-value");
        connection.ApiToken.Should().Be("token-value");
        connection.AccountId.Should().BeNull();
        connection.Password.Should().Be("stored-password");
    }

    /// <summary>A server with no stored password maps a connection with none, and the secrets are masked when it is printed.</summary>
    [Fact]
    public void ToPanelConnection_NoPassword_IsNull_AndTheConnectionPrintsNoSecret()
    {
        var dto = new ServerConnectionDto(5, "srv", ServerModules.CPanel, "panel.example.test", null, "hash-value", "root", "token-value");

        var connection = dto.ToPanelConnection(null);

        connection.Password.Should().BeNull();
        connection.UseSsl.Should().BeFalse();
        dto.ToPanelConnection("stored-password").ToString().Should().NotContain("stored-password").And.NotContain("hash-value").And.NotContain("token-value");
    }

    /// <summary>The entity's mapper carries the SSL choice and no password: only a plugin call reads that.</summary>
    [Fact]
    public void ToConnection_CarriesTheSslChoice_AndNoPassword()
    {
        var server = Server.Create(
            new ServerDetails(
                "srv", "panel.example.test", null, null, ServerModules.CPanel, "admin",
                new ServerCredentials("stored-password", "token-value", "hash-value"), true, null, false, false, 0m, null, null,
                ServerNameServers.None,
                false),
            TestMoments.Seeded);

        var dto = server.ToConnection();

        dto.UseSsl.Should().BeTrue();
        dto.Module.Should().Be(ServerModules.CPanel);
        dto.ToString().Should().NotContain("stored-password");
        typeof(ServerConnectionDto).GetProperties().Select(p => p.Name).Should().NotContain("Password");
    }
}
