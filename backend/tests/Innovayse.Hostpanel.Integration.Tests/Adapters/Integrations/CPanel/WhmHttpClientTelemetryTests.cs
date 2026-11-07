using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Resilience.Extensions;
using Innovayse.Hostpanel.Infrastructure.Resilience.Settings;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// Proves that nothing the named WHM client's registration adds (request logging removed, the
/// resilience pipeline with its telemetry) writes the request URL or the password to a log.
/// </summary>
public sealed class WhmHttpClientTelemetryTests
{
    /// <summary>A call through the real registration shape logs neither the URL nor the password, and sends the password in the body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateAccount_ThroughTheNamedClient_LogsNeitherTheUrlNorThePasswordAsync()
    {
        await using var whm = await StubHttpServer.StartAsync();
        whm.Enqueue(WhmReplies.Created);
        var logs = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddOptions<HttpResilienceOptions>();

        // The same chain DependencyInjection registers for HttpClientNames.CPanelWhm, minus the
        // outbound address guard, which refuses the loopback address the stub listens on.
        services.AddHttpClient(HttpClientNames.CPanelWhm, client => client.Timeout = HttpBackstopTimeouts.CPanelWhm)
            .RemoveAllLoggers()
            .AddNoRetryResilience(o => o.CPanelWhm);
        await using var provider = services.BuildServiceProvider();
        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientNames.CPanelWhm);
        var client = new WhmApiClient(http, provider.GetRequiredService<ILoggerFactory>().CreateLogger<WhmApiClient>());
        var connection = new WhmConnection(whm.BaseAddress.ToString().TrimEnd('/'), "root", "api-token-value");

        var reply = await client.CreateAccountAsync(connection, "testuser", "example.com", "S3cret-pass", "Basic", CancellationToken.None);

        reply.IsSuccess.Should().BeTrue();
        whm.Requests.Should().ContainSingle().Which.PathAndQuery.Should().NotContain("S3cret-pass");
        foreach (var line in logs.Entries)
        {
            line.Message.Should().NotContain("S3cret-pass").And.NotContain("api-token-value").And.NotContain("createacct").And.NotContain("json-api");
        }
    }
}
