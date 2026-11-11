using System.Net;
using System.Net.Sockets;
using System.Text;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;
using Microsoft.Extensions.Logging.Abstractions;
using Renci.SshNet;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.SshAgent;

/// <summary>The setup adapters: key generation, the bundle and the host key scanner.</summary>
public sealed class SshAgentSetupAdapterTests
{
    /// <summary>Each generated pair is new, loads as an ED25519 key, and its comment is stripped to safe characters.</summary>
    [Fact]
    public void Generator_ProducesDistinctLoadableEd25519Pairs()
    {
        var generator = new SshIdentityGenerator();

        var a = generator.Generate("hostpanel-server-1");
        var b = generator.Generate("hostpanel-server-1; rm -rf /");

        a.PublicKey.Should().NotBe(b.PublicKey);
        b.PublicKey.Should().EndWith("hostpanel-server-1rm-rf");
        a.PrivateKey.Should().StartWith("-----BEGIN OPENSSH PRIVATE KEY-----\n");
        using var keyFile = new PrivateKeyFile(new MemoryStream(Encoding.UTF8.GetBytes(a.PrivateKey)));
        keyFile.Key.ToString().Should().Be("ssh-ed25519");
        a.PublicKey.Split(' ')[1].Should().Be(Convert.ToBase64String(((Renci.SshNet.Security.KeyHostAlgorithm)keyFile.HostKeyAlgorithms.First()).Data));
    }

    /// <summary>Two bundles built from the same files are byte-identical and carry the agent version.</summary>
    [Fact]
    public void Bundle_IsDeterministicAndVersioned()
    {
        var first = new EmbeddedSshAgentBundle();
        var second = new EmbeddedSshAgentBundle();

        first.Sha256.Should().Be(second.Sha256).And.MatchRegex("^[0-9a-f]{64}$");
        first.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        first.Profiles.Should().BeEquivalentTo(["cwp", "generic-linux"]);
    }

    /// <summary>Nothing listening: the connection is refused, so the port or a rejecting firewall is at fault.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Scanner_ReportsAClosedPortAsRefused()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var probe = await Scanner().ScanAsync(new SshEndpoint("127.0.0.1", port, TimeSpan.FromSeconds(2)), CancellationToken.None);

        probe.Key.Should().BeNull();
        probe.Failure.Should().Be(SshEndpointFailure.Refused);
    }

    /// <summary>A port that accepts and hangs up before any banner: sshd turning the address away.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Scanner_ReportsAHangUpBeforeTheBannerAsClosedBeforeGreeting()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = 0;
        var server = Task.Run(async () =>
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref accepted);
            }
        });

        var probe = await Scanner().ScanAsync(new SshEndpoint("127.0.0.1", port, TimeSpan.FromSeconds(3)), CancellationToken.None);
        listener.Stop();
        await server.ContinueWith(_ => { }, TaskScheduler.Default);

        // Settled from the SSH failure alone: a turned-away address is not asked a second time.
        accepted.Should().Be(1);

        probe.Key.Should().BeNull();
        probe.Failure.Should().Be(SshEndpointFailure.ClosedBeforeGreeting);
    }

    /// <summary>No answer at all within the timeout: no network path (VPN, routing, a dropping firewall).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Scanner_ReportsSilenceAsNoAnswer()
    {
        // TEST-NET-1 (RFC 5737) is never routed, so the connection gets no answer.
        var probe = await Scanner().ScanAsync(new SshEndpoint("192.0.2.1", 22, TimeSpan.FromSeconds(1)), CancellationToken.None);

        probe.Key.Should().BeNull();
        probe.Failure.Should().Be(SshEndpointFailure.NoAnswer);
    }

    /// <summary>The scanner under test.</summary>
    /// <returns>A scanner with a silent logger.</returns>
    private static SshHostKeyScanner Scanner() => new(NullLogger<SshHostKeyScanner>.Instance);
}
