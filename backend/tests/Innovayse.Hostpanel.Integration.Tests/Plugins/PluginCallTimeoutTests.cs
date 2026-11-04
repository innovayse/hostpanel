using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Innovayse.SDK.Plugins.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The per call time limit of a plugin route, through the real pipeline against Postgres and the test-only plugins of
/// <see cref="AppPluginFixture"/>: a plugin that never answers is cancelled through its token and the caller gets
/// <c>PLUGIN_TIMEOUT</c> with none of the plugin's text, a plugin that ignores the token is abandoned within a bound and
/// counted, one that answers in time is not touched, and a stream is cut only by its own silence, never by its length.
/// Each test derives its host with the limits it needs.
/// </summary>
public sealed class PluginCallTimeoutTests : IClassFixture<AppPluginFixture>, IDisposable
{
    /// <summary>The short limit of the cases that never answer, in seconds; the least the options allow.</summary>
    private const int ShortLimitSeconds = 1;

    /// <summary>The limit of the cases that answer in time or keep streaming, in seconds; room for a slow runner.</summary>
    private const int LongLimitSeconds = 3;

    /// <summary>How long a test waits for something that should already be on its way before it calls it stuck.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>The log position at the start of this test, so a line logged by an earlier test of the class is not counted.</summary>
    private readonly int _logMark;

    /// <summary>The hosts this test derived, disposed with it.</summary>
    private readonly List<WebApplicationFactory<Program>> _hosts = [];

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes, and marks the log.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginCallTimeoutTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
        _logMark = _fixture.LogMark();
    }

    /// <summary>Releases the derived hosts.</summary>
    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Dispose();
        }
    }

    /// <summary>Derives a host of the fixture whose plugin call options are set for the test.</summary>
    /// <param name="limitSeconds">The call limit.</param>
    /// <param name="configure">More options to set, or <see langword="null"/>.</param>
    /// <param name="configureServices">More services to replace, or <see langword="null"/>.</param>
    /// <returns>The host, disposed with the test.</returns>
    private WebApplicationFactory<Program> Timed(
        int limitSeconds, Action<PluginApiOptions>? configure = null, Action<IServiceCollection>? configureServices = null)
    {
        var host = _fixture.Derive(services =>
        {
            services.Configure<PluginApiOptions>(options =>
            {
                options.CallTimeoutSeconds = limitSeconds;
                options.AbandonWaitSeconds = 1;
                configure?.Invoke(options);
            });
            configureServices?.Invoke(services);
        });
        _hosts.Add(host);
        return host;
    }

    /// <summary>Registers an account holder and makes their client of a host.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <param name="label">Distinguishes the registered address.</param>
    /// <returns>A client carrying the account holder's token.</returns>
    private async Task<HttpClient> OwnerAsync(WebApplicationFactory<Program> host, string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, label);
        return AppPluginFixture.WithBearerOf(host, owner.Client);
    }

    /// <summary>Makes an administrator's client of a host.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <returns>The client.</returns>
    private async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> host)
    {
        using var admin = await _fixture.AdminClientAsync();
        return AppPluginFixture.WithBearerOf(host, admin);
    }

    /// <summary>Calls a client area route of the plugin and returns as soon as the response headers are in.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> ClientCallAsync(HttpClient http, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, $"/api/client/plugins/{_fixture.OtherPluginId}/{path}")
        {
            Content = method == HttpMethod.Post ? new StringContent("{}", Encoding.UTF8, "application/json") : null,
        };
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).WaitAsync(Patience);
    }

    /// <summary>A stream that sends an event every interval, for a number of events, then ends.</summary>
    /// <param name="count">How many events.</param>
    /// <param name="interval">The pause before each event.</param>
    /// <param name="ct">Cancelled when the stream is ended early.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> Steady(int count, TimeSpan interval, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 1; i <= count; i++)
        {
            await Task.Delay(interval, ct);
            yield return new PluginEvent($"tick-{i}");
        }
    }

    /// <summary>A stream that sends one event, then says nothing until its token is cancelled.</summary>
    /// <param name="cancelled">Completed when the stream sees its token cancelled.</param>
    /// <param name="ct">Cancelled when the host ends the stream.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> GoesSilent(TaskCompletionSource cancelled, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new PluginEvent("first");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException)
        {
            cancelled.TrySetResult();
            throw;
        }
    }

    /// <summary>A plugin that never answers is cancelled through its token and the caller gets 504 with no plugin text, the id is logged and the audit line stays.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatNeverAnswers_IsCancelled_AndTheCallerGetsATimeout()
    {
        using var http = await OwnerAsync(Timed(ShortLimitSeconds), "timeout-never");
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.AsyncBehaviour = async (_, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }

            throw new InvalidOperationException("secret-detail-of-the-plugin");
        };

        using var response = await ClientCallAsync(http, HttpMethod.Post, "notes");

        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_TIMEOUT);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret-detail");
        await cancelled.Task.WaitAsync(Patience);
        _fixture.LoggedWarning(_fixture.OtherPluginId, "did not answer", _logMark).Should().BeTrue();

        var audit = await _fixture.ActivityOfAsync(_fixture.OtherPluginId);
        audit.Should().ContainSingle().Which.Description.Should().Contain("Called Plugin Route").And.Contain("POST /notes");
    }

    /// <summary>The same limit applies to an admin call.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnAdminCall_IsLimitedToo()
    {
        using var http = await AdminAsync(Timed(ShortLimitSeconds));
        _fixture.Probe.AsyncBehaviour = async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return PluginReply.NoContent();
        };

        using var response = await http.GetAsync($"/api/admin/plugins/{_fixture.OtherPluginId}/ext/stats").WaitAsync(Patience);

        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_TIMEOUT);
    }

    /// <summary>A plugin that ignores its token is abandoned after the limit and the abandon wait, no sooner and not unbounded, and the caller gets the timeout.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatIgnoresItsToken_IsAbandoned_WithinTheBound()
    {
        using var http = await OwnerAsync(Timed(ShortLimitSeconds), "timeout-ignores");
        var release = new TaskCompletionSource<PluginReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.AsyncBehaviour = (_, _) => release.Task;

        try
        {
            var clock = Stopwatch.StartNew();
            using var response = await ClientCallAsync(http, HttpMethod.Get, "ping");
            clock.Stop();

            response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_TIMEOUT);
            _fixture.LoggedWarning(_fixture.OtherPluginId, "ignored its cancellation", _logMark).Should().BeTrue();

            // The limit (1 s) plus the abandon wait (1 s) have passed, and the call did not wait for the plugin forever.
            clock.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)).And.BeLessThan(TimeSpan.FromSeconds(20));
        }
        finally
        {
            release.TrySetException(new InvalidOperationException("secret-detail-of-the-plugin"));
        }
    }

    /// <summary>A plugin that catches the cancellation and still returns a reply after the limit is answered with the timeout, not with its reply.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatAnswersAfterTheLimit_IsStillATimeout()
    {
        using var http = await OwnerAsync(Timed(ShortLimitSeconds), "timeout-late-reply");
        _fixture.Probe.AsyncBehaviour = async (_, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                // Caught on purpose: the plugin answers although its time is up.
            }

            return PluginReply.Json(new { late = "secret-late-reply" });
        };

        using var response = await ClientCallAsync(http, HttpMethod.Get, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_TIMEOUT);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret-late-reply");
        _fixture.LoggedWarning(_fixture.OtherPluginId, "did not answer", _logMark).Should().BeTrue();
    }

    /// <summary>A plugin that holds as many abandoned calls as the limit allows is refused at once, without being called, until one of them ends.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginAtItsAbandonedLimit_IsRefusedWithoutBeingCalled()
    {
        using var http = await OwnerAsync(Timed(ShortLimitSeconds, options => options.MaxAbandonedCallsPerPlugin = 1), "timeout-abandoned-cap");
        var release = new TaskCompletionSource<PluginReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.AsyncBehaviour = (_, _) => release.Task;

        try
        {
            using var first = await ClientCallAsync(http, HttpMethod.Get, "ping");
            first.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
            _fixture.Probe.Calls.Should().HaveCount(1);

            using var refused = await ClientCallAsync(http, HttpMethod.Get, "ping");

            refused.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
            await ClientsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.PLUGIN_TIMEOUT);
            _fixture.Probe.Calls.Should().HaveCount(1);
            _fixture.LoggedWarning(_fixture.OtherPluginId, "abandoned calls running", _logMark).Should().BeTrue();
        }
        finally
        {
            release.TrySetResult(PluginReply.NoContent());
        }
    }

    /// <summary>A plugin that answers just inside the limit is answered normally.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatAnswersJustInTime_IsAnsweredNormally()
    {
        using var http = await OwnerAsync(Timed(LongLimitSeconds), "timeout-in-time");
        _fixture.Probe.AsyncBehaviour = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return PluginReply.Json(new { ok = true });
        };

        using var response = await ClientCallAsync(http, HttpMethod.Get, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("true");
    }

    /// <summary>A stream that keeps sending past the limit is not cut: its length is not the limit's concern.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamThatKeepsSending_PastTheLimit_IsNotCut()
    {
        using var http = await OwnerAsync(Timed(LongLimitSeconds), "timeout-stream-busy");
        _fixture.Probe.AsyncBehaviour = (_, _) => Task.FromResult(
            PluginReply.Stream(Steady(3, TimeSpan.FromSeconds(1.5), CancellationToken.None)));

        using var response = await ClientCallAsync(http, HttpMethod.Get, "stream");
        var body = await response.Content.ReadAsStringAsync().WaitAsync(Patience);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("data: tick-1").And.Contain("data: tick-3");
        body.Should().NotContain(HostpanelErrorCodes.PLUGIN_TIMEOUT);
    }

    /// <summary>
    /// A stream that goes silent past the limit is cancelled and ended with the timeout error and <c>done</c>, although the
    /// host's heartbeat, set below the limit, kept writing: a heartbeat does not keep a silent producer alive.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamThatGoesSilent_PastTheLimit_IsCut_EvenWithHeartbeats()
    {
        using var http = await OwnerAsync(Timed(LongLimitSeconds, options => options.StreamHeartbeatSeconds = 1), "timeout-stream-silent");
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.AsyncBehaviour = (_, _) => Task.FromResult(PluginReply.Stream(GoesSilent(cancelled, CancellationToken.None)));

        using var response = await ClientCallAsync(http, HttpMethod.Get, "stream");
        var body = await response.Content.ReadAsStringAsync().WaitAsync(Patience);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("data: first");
        body.Should().Contain(ServerSentEvent.Heartbeat.TrimEnd());
        body.Should().Contain("event: error").And.Contain(HostpanelErrorCodes.PLUGIN_TIMEOUT).And.Contain("event: done");
        await cancelled.Task.WaitAsync(Patience);
        _fixture.LoggedWarning(_fixture.OtherPluginId, "stayed silent", _logMark).Should().BeTrue();
    }

    /// <summary>The concurrent stream lease is released when a stream is ended by the timeout: the next call is not refused with 429.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamEndedByTheTimeout_ReleasesItsLease()
    {
        var host = Timed(
            ShortLimitSeconds,
            configureServices: services =>
            {
                services.RemoveAll<PluginRateLimiter>();
                services.AddSingleton(new PluginRateLimiter(
                    new RateLimitOptions
                    {
                        PluginDefault = new RateLimitTierOptions { PermitLimit = 1000, WindowSeconds = 60 },
                        PluginStreamPermitLimit = 1,
                    },
                    disabled: false));
            });
        using var http = await OwnerAsync(host, "timeout-stream-lease");
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.AsyncBehaviour = (_, _) => Task.FromResult(PluginReply.Stream(GoesSilent(cancelled, CancellationToken.None)));

        using (var first = await ClientCallAsync(http, HttpMethod.Get, "stream"))
        {
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            (await first.Content.ReadAsStringAsync().WaitAsync(Patience)).Should().Contain(HostpanelErrorCodes.PLUGIN_TIMEOUT);
        }

        // The lease goes when the server finishes the response, which can be just after the client read its end: a bounded retry.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        HttpResponseMessage next;
        while ((next = await ClientCallAsync(http, HttpMethod.Get, "stream")).StatusCode == HttpStatusCode.TooManyRequests
            && DateTime.UtcNow < deadline)
        {
            next.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        using (next)
        {
            next.StatusCode.Should().Be(HttpStatusCode.OK);
            (await next.Content.ReadAsStringAsync().WaitAsync(Patience)).Should().Contain(HostpanelErrorCodes.PLUGIN_TIMEOUT);
        }
    }
}
