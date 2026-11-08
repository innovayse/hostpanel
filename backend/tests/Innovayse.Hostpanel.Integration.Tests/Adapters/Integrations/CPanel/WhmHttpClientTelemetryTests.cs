using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// Proves that nothing the named WHM client's registration adds (request logging removed, the
/// resilience pipeline with its telemetry) writes the request URL or the password to a log. The
/// client is built by <see cref="CPanelWhmHttpClientExtensions.AddCPanelWhmHttpClient"/>, the call
/// the application makes, so taking <c>RemoveAllLoggers</c> out of it turns this test red.
/// </summary>
public sealed class WhmHttpClientTelemetryTests
{
    /// <summary>A call through the real registration logs neither the URL nor the password, and sends the password in the body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateAccount_ThroughTheNamedClient_LogsNeitherTheUrlNorThePasswordAsync()
    {
        await using var whm = await StubHttpServer.StartAsync();
        whm.Enqueue(WhmReplies.Created);
        await using var host = WhmTestHost.Build();

        var reply = await host.Client().CreateAccountAsync(
            WhmTestHost.ConnectionTo(whm), "testuser", "example.com", "S3cret-pass", "Basic", CancellationToken.None);

        reply.IsSuccess.Should().BeTrue();
        whm.Requests.Should().ContainSingle().Which.PathAndQuery.Should().NotContain("S3cret-pass");
        foreach (var line in host.Logs.Entries)
        {
            line.Message.Should()
                .NotContain("S3cret-pass").And.NotContain("api-token-value")
                .And.NotContain("createacct").And.NotContain("json-api").And.NotContain(WhmTestHost.PanelHost);
        }
    }
}
