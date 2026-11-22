using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Servers;

/// <summary>Unit tests for <see cref="Server"/>.</summary>
public sealed class ServerTests
{
    /// <summary>The fixed moment every test acts at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Create copies every detail onto the server and raises <see cref="ServerCreatedEvent"/>.</summary>
    [Fact]
    public void Create_AppliesTheDetailsAndRaisesTheEvent()
    {
        var server = Server.Create(Details("web-1"), Now);

        server.Name.Should().Be("web-1");
        server.Hostname.Should().Be("web-1.example.test");
        server.Module.Should().Be(ServerModules.CPanel);
        server.Username.Should().Be("root");
        server.Password.Should().Be("secret");
        server.MaxAccounts.Should().Be(100);
        server.Ns1Hostname.Should().Be("ns1.example.test");
        server.ServerGroupId.Should().BeNull();
        server.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServerCreatedEvent>()
            .Which.Should().Match<ServerCreatedEvent>(e => e.Name == "web-1" && e.OccurredAt == Now);
    }

    /// <summary>A new server trusts no self signed certificate unless its details say so.</summary>
    [Fact]
    public void Create_TrustsNoSelfSignedCertificateByDefault()
    {
        Server.Create(Details("web-1"), Now).AcceptsSelfSignedCertificate.Should().BeFalse();
        Server.Create(Details("web-1") with { AcceptsSelfSignedCertificate = true }, Now).AcceptsSelfSignedCertificate.Should().BeTrue();
    }

    /// <summary>A reconfiguration sets the choice from its details, so the operator can switch it on and back off.</summary>
    [Fact]
    public void Reconfigure_SwitchesTheSelfSignedCertificateChoice()
    {
        var server = Server.Create(Details("web-1"), Now);

        server.Reconfigure(Details("web-1") with { AcceptsSelfSignedCertificate = true }, ServerCredentialClears.None);
        server.AcceptsSelfSignedCertificate.Should().BeTrue();

        server.Reconfigure(Details("web-1"), ServerCredentialClears.None);
        server.AcceptsSelfSignedCertificate.Should().BeFalse();
    }

    /// <summary>A server without SSL cannot accept a self signed certificate, on create and on reconfigure, and keeps its stored choice.</summary>
    [Fact]
    public void SelfSignedCertificate_WithoutSsl_IsRefused()
    {
        var withoutSsl = Details("web-1") with { UseSSL = false, AcceptsSelfSignedCertificate = true };

        var create = () => Server.Create(withoutSsl, Now);
        create.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServerErrors.SelfSignedRequiresSsl);

        var server = Server.Create(Details("web-1") with { AcceptsSelfSignedCertificate = true }, Now);
        var reconfigure = () => server.Reconfigure(withoutSsl, ServerCredentialClears.None);
        reconfigure.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServerErrors.SelfSignedRequiresSsl);
        server.UseSSL.Should().BeTrue();
        server.AcceptsSelfSignedCertificate.Should().BeTrue();

        Server.Create(withoutSsl with { AcceptsSelfSignedCertificate = false }, Now).UseSSL.Should().BeFalse();
    }

    /// <summary>A reconfiguration without new credentials keeps the stored ones, so editing a server never wipes its secrets.</summary>
    [Fact]
    public void Reconfigure_KeepsCredentialsWhenNoneAreGiven()
    {
        var server = Server.Create(Details("web-1"), Now);

        server.Reconfigure(Details("web-2") with { Credentials = ServerCredentials.None }, ServerCredentialClears.None);

        server.Name.Should().Be("web-2");
        server.Password.Should().Be("secret");
        server.ApiToken.Should().Be("token");
        server.AccessHash.Should().Be("hash");
    }

    /// <summary>A reconfiguration with new credentials replaces them.</summary>
    [Fact]
    public void Reconfigure_ReplacesCredentialsWhenGiven()
    {
        var server = Server.Create(Details("web-1"), Now);

        server.Reconfigure(Details("web-1") with { Credentials = new ServerCredentials("new-secret", "token", "hash") }, ServerCredentialClears.None);

        server.Password.Should().Be("new-secret");
    }

    /// <summary>An empty or whitespace credential is "unchanged", never "erase".</summary>
    [Fact]
    public void Reconfigure_KeepsCredentialsWhenBlank()
    {
        var server = Server.Create(Details("web-1"), Now);

        server.Reconfigure(Details("web-1") with { Credentials = new ServerCredentials(string.Empty, "   ", "\t") }, ServerCredentialClears.None);

        server.Password.Should().Be("secret");
        server.ApiToken.Should().Be("token");
        server.AccessHash.Should().Be("hash");
        server.HasPassword.Should().BeTrue();
    }

    /// <summary>An explicit clear removes the credential, and wins over a value sent alongside it.</summary>
    [Fact]
    public void Reconfigure_ClearRemovesTheCredential()
    {
        var server = Server.Create(Details("web-1"), Now);

        server.Reconfigure(
            Details("web-1") with { Credentials = new ServerCredentials("ignored-because-cleared", "token", "hash") },
            new ServerCredentialClears(Password: true, ApiToken: false, AccessHash: true));

        server.Password.Should().BeNull();
        server.HasPassword.Should().BeFalse();
        server.ApiToken.Should().Be("token");
        server.HasApiToken.Should().BeTrue();
        server.AccessHash.Should().BeNull();
        server.HasAccessHash.Should().BeFalse();
    }

    /// <summary>A server joins a group and can leave it again.</summary>
    [Fact]
    public void AssignToGroup_ThenRemoveFromGroup_ClearsTheGroup()
    {
        var server = Server.Create(Details("web-1"), Now);

        server.AssignToGroup(7);
        server.ServerGroupId.Should().Be(7);

        server.RemoveFromGroup();
        server.ServerGroupId.Should().BeNull();
    }

    /// <summary>A connection test records the outcome, the account count and when it ran.</summary>
    [Fact]
    public void RecordConnectionTest_StoresTheOutcome()
    {
        var server = Server.Create(Details("web-1"), Now);

        var testedAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        server.RecordConnectionTest(isOnline: true, accountsCount: 12, testedAt);

        server.IsOnline.Should().BeTrue();
        server.AccountsCount.Should().Be(12);
        server.LastTestedAt.Should().Be(testedAt);
    }

    /// <summary>Builds a complete set of server details.</summary>
    /// <param name="name">The server name; the hostname is derived from it.</param>
    /// <returns>The details.</returns>
    private static ServerDetails Details(string name)
    {
        return new ServerDetails(
            name,
            $"{name}.example.test",
            "192.0.2.10",
            null,
            ServerModules.CPanel,
            "root",
            new ServerCredentials("secret", "token", "hash"),
            UseSSL: true,
            MaxAccounts: 100,
            IsDefault: false,
            IsDisabled: false,
            MonthlyCost: 20m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None with { Ns1Hostname = "ns1.example.test", Ns1Ip = "192.0.2.1" },
            AcceptsSelfSignedCertificate: false);
    }
}
