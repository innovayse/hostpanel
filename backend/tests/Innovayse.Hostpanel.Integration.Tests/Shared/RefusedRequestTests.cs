using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// A request the authorization refuses is answered once: one well formed error body, and no "Response already started" warning
/// (a second writer meeting the started response logged it for what was an ordinary 401 or 403).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RefusedRequestTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A signed-in user who owns no client record and holds no client role is refused with one 403 body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SignedInUserWithoutClientRecordIsRefusedOnceAsync()
    {
        var logs = new RecordingLoggerProvider();
        using var host = HostWith(logs);
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "refused-once");
        using var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = user.Client.DefaultRequestHeaders.Authorization;

        var response = await http.GetAsync("/api/clients/me");

        await AssertAnsweredOnceAsync(response, HttpStatusCode.Forbidden, SharedErrorCodes.Forbidden, logs);
    }

    /// <summary>A caller with no credential is refused with one 401 body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnonymousCallerIsRefusedOnceAsync()
    {
        var logs = new RecordingLoggerProvider();
        using var host = HostWith(logs);
        using var http = host.CreateClient();

        var response = await http.GetAsync("/api/clients/me");

        await AssertAnsweredOnceAsync(response, HttpStatusCode.Unauthorized, SharedErrorCodes.Unauthorized, logs);
    }

    /// <summary>
    /// The handler that answers a refusal is the product's own, whatever else registers one later: the framework's
    /// stock handler forbids or challenges every scheme of the policy in turn, and the second one to set a status
    /// on the started response throws.
    /// </summary>
    [Fact]
    public void TheHandlerThatAnswersARefusalIsTheProductsOwn()
    {
        var handler = factory.Services.GetRequiredService<IAuthorizationMiddlewareResultHandler>();

        handler.Should().BeOfType<IdentityPluginUnavailableResponder>();
    }

    /// <summary>Derives a host that records everything it logs, at every level.</summary>
    /// <param name="logs">Where the host's log lines are kept.</param>
    /// <returns>The derived host.</returns>
    private WebApplicationFactory<Program> HostWith(RecordingLoggerProvider logs) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(logs);
        }));

    /// <summary>Asserts the answer is one problem+json body carrying the expected code, and the host logged no warning.</summary>
    /// <param name="response">The answer.</param>
    /// <param name="status">The status it must carry.</param>
    /// <param name="code">The error code the body must name.</param>
    /// <param name="logs">What the host logged while it answered.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertAnsweredOnceAsync(
        HttpResponseMessage response, HttpStatusCode status, string code, RecordingLoggerProvider logs)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/problem+json"));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(code);
        body.RootElement.GetProperty("status").GetInt32().Should().Be((int)status);
        body.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();

        var lateWrites = logs.Entries.Where(entry => entry.Message.Contains("Response already started", StringComparison.Ordinal)).ToList();
        lateWrites.Should().BeEmpty(
            "a refusal is written once; the exception met afterwards was: {0}",
            string.Join(" | ", lateWrites.Select(entry => entry.Exception)));
    }
}
