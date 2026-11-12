using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>Tests how <see cref="ServerConnectionTester"/> reports a cPanel server it cannot reach and what it logs about it.</summary>
public sealed class ServerConnectionTesterCPanelTests
{
    /// <summary>A host name that cannot be a URL fails the test as unreachable and logs only the exception type.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_InvalidHostName_IsUnreachableAndLogsOnlyTheExceptionTypeAsync()
    {
        var logs = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddHttpClient();
        await using var provider = services.BuildServiceProvider();
#pragma warning disable CS0618 // Legacy HostingPanel contract, obsolete since SDK 3.1.0; the tester still takes the CWP ports.
        var tester = new ServerConnectionTester(
            new FakeHostingPanelApiClient(),
            new FakeHostingPanelV7ApiClient(),
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<ILoggerFactory>(),
            Options.Create(new ProvisioningOptions()),
            provider.GetRequiredService<ILogger<ServerConnectionTester>>());
#pragma warning restore CS0618
        var server = Server.Create(
            new ServerDetails(
                "bad-host", "bad host", null, null, ServerModule.CPanel, "root",
                new ServerCredentials(null, "secret-token-value", null), true, null, false, false, 0m, null, null,
                ServerNameServers.None),
            TestMoments.Seeded);

        var result = await tester.TestAsync(server, CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE);
        var lines = logs.Entries.Where(e => e.Message.StartsWith("cPanel connection test failed", StringComparison.Ordinal)).ToList();
        lines.Should().ContainSingle();
        lines[0].Exception.Should().BeNull();
        lines[0].Message.Should().Contain("InvalidOperationException").And.NotContain("secret-token-value");
    }
}
