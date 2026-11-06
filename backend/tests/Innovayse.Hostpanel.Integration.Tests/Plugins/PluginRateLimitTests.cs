using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Hostpanel.Sdk.Plugins.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The rate limit of the plugin doors (<c>PluginRateLimitFilter</c>), against Postgres and the test-only plugins of
/// <see cref="AppPluginFixture"/>: each tier a route can ask for refuses with the standard 429 once its budget for one
/// plugin and one caller is spent, the budget is not shared with another plugin or another caller, and a streaming route
/// is also held to the number of streams the caller has open. The Testing host does not limit, so each test derives a host
/// whose <see cref="PluginRateLimiter"/> is switched on with tiny budgets.
/// </summary>
public sealed class PluginRateLimitTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The requests a tier allows in these tests, small so a test spends it with a few calls.</summary>
    private const int Permits = 2;

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>The log position at the start of this test, so a refusal logged by an earlier test of the class is not counted.</summary>
    private readonly int _logMark;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes, and marks the log.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginRateLimitTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
        _logMark = _fixture.LogMark();
    }

    /// <summary>The budgets of the derived hosts: two requests a tier, one open stream.</summary>
    /// <param name="defaultPermits">The requests the default tier allows.</param>
    /// <returns>The options, with every tier a plugin route can ask for sized for the test.</returns>
    private static RateLimitOptions Limits(int defaultPermits)
    {
        return new RateLimitOptions
        {
            PluginDefault = new RateLimitTierOptions { PermitLimit = defaultPermits, WindowSeconds = 60 },
            PluginStrict = new RateLimitTierOptions { PermitLimit = 1, WindowSeconds = 60 },
            PluginHeavy = new RateLimitTierOptions { PermitLimit = 1, WindowSeconds = 60 },
            PluginStreamPermitLimit = 1,
        };
    }

    /// <summary>Derives a host whose plugin limiter is switched on.</summary>
    /// <param name="defaultPermits">The requests the default tier allows; more than a test makes when only the stream limit is under test.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    private WebApplicationFactory<Program> LimitedHost(int defaultPermits = Permits)
    {
        return _fixture.Derive(services =>
        {
            services.RemoveAll<PluginRateLimiter>();
            services.AddSingleton(new PluginRateLimiter(Limits(defaultPermits), disabled: false));
        });
    }

    /// <summary>
    /// Calls a client area route, trying again for as long as the answer is the stream refusal. The lease of a finished
    /// stream is released when its response is disposed, which can run after the client has read the end of the body, so
    /// the next call may meet the allowance still taken for a moment; a bounded retry waits for the release without a fixed sleep.
    /// </summary>
    /// <param name="http">The caller.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <returns>The first answer that is not a 429, or the last 429 once the wait is spent.</returns>
    private async Task<HttpResponseMessage> CallOnceReleasedAsync(HttpClient http, string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (true)
        {
            var response = await ClientCallAsync(http, path);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || DateTime.UtcNow >= deadline)
            {
                return response;
            }

            response.Dispose();

            // The lease goes when the server finishes the response, which can be just after the client read its end;
            // a short pause keeps the retries from spending the tier budget while that happens.
            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }

    /// <summary>Registers an account holder and makes their client of a host.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <param name="label">Distinguishes the registered address.</param>
    /// <returns>The account holder, and a client of <paramref name="host"/> carrying their token.</returns>
    private async Task<(SignedInClient Owner, HttpClient Http)> OwnerAsync(WebApplicationFactory<Program> host, string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, label);
        return (owner, AppPluginFixture.WithBearerOf(host, owner.Client));
    }

    /// <summary>Makes a client of a host carrying an administrator's token.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <returns>The client.</returns>
    private async Task<HttpClient> AdminOfAsync(WebApplicationFactory<Program> host)
    {
        using var admin = await _fixture.AdminClientAsync();
        return AppPluginFixture.WithBearerOf(host, admin);
    }

    /// <summary>Calls a client area route of the fixture's plugin.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <param name="pluginId">The plugin to call; the fixture's main plugin when omitted.</param>
    /// <returns>The response.</returns>
    private Task<HttpResponseMessage> ClientCallAsync(HttpClient http, string path, string? pluginId = null)
    {
        return http.GetAsync($"/api/client/plugins/{pluginId ?? _fixture.PluginId}/{path}");
    }

    /// <summary>Calls an admin area route of the fixture's plugin.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="path">The path below the plugin's admin prefix.</param>
    /// <returns>The response.</returns>
    private Task<HttpResponseMessage> AdminCallAsync(HttpClient http, string path)
    {
        return http.GetAsync($"/api/admin/plugins/{_fixture.PluginId}/ext/{path}");
    }

    /// <summary>
    /// Asserts a response is the platform's 429 for a policy: problem+json with the rate limit code, and the limiter's
    /// headers: <c>Retry-After</c>, and <c>X-RateLimit-Remaining</c> of 0, a positive <c>X-RateLimit-Limit</c>, an
    /// <c>X-RateLimit-Reset</c> timestamp and the <c>X-RateLimit-Policy</c> that refused. The same policy is read from the
    /// warning the limiter logs.
    /// </summary>
    /// <param name="response">The refusal.</param>
    /// <param name="policy">The policy that must have refused.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    private async Task AssertRefusedAsync(HttpResponseMessage response, string policy)
    {
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.GetValues("X-RateLimit-Remaining").Should().ContainSingle().Which.Should().Be("0");
        response.Headers.GetValues("X-RateLimit-Policy").Should().ContainSingle().Which.Should().Be(policy);
        long.Parse(response.Headers.GetValues("X-RateLimit-Limit").Single(), CultureInfo.InvariantCulture).Should().BePositive();
        // Reset is the moment of the refusal plus Retry-After, so taking Retry-After off gives back a moment within seconds of now.
        var reset = long.Parse(response.Headers.GetValues("X-RateLimit-Reset").Single(), CultureInfo.InvariantCulture);
        var retryAfterSeconds = (long)response.Headers.RetryAfter!.Delta!.Value.TotalSeconds;
        (reset - retryAfterSeconds).Should().BeCloseTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 30);
        _fixture.LoggedRateLimitRefusal(policy, _fixture.PluginId, _logMark).Should().BeTrue();
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.RateLimited);
    }

    /// <summary>Spends a route's budget, asserting each call is served.</summary>
    /// <param name="call">Makes one call.</param>
    /// <param name="permits">How many calls the budget allows.</param>
    /// <returns>A task that completes when the budget is spent.</returns>
    private static async Task SpendAsync(Func<Task<HttpResponseMessage>> call, int permits)
    {
        for (var i = 0; i < permits; i++)
        {
            using var served = await call();
            served.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    /// <summary>A route that names no tier is measured against <c>plugin-default</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DefaultTier_RefusesWith429AfterItsBudget()
    {
        using var host = LimitedHost();
        var (_, http) = await OwnerAsync(host, "plugin-limit-default");

        await SpendAsync(() => ClientCallAsync(http, "ping"), Permits);
        using var refused = await ClientCallAsync(http, "ping");

        await AssertRefusedAsync(refused, RateLimitPolicies.PluginDefault);

        // A refused request never reached the plugin: only the permitted calls did.
        _fixture.Probe.Calls.Should().HaveCount(Permits);
    }

    /// <summary>A route that asks for the strict tier is measured against <c>plugin-strict</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StrictTier_RefusesWith429AfterItsBudget()
    {
        using var host = LimitedHost();
        var (_, http) = await OwnerAsync(host, "plugin-limit-strict");

        await SpendAsync(() => ClientCallAsync(http, "strict-ping"), 1);
        using var refused = await ClientCallAsync(http, "strict-ping");

        await AssertRefusedAsync(refused, RateLimitPolicies.PluginStrict);
    }

    /// <summary>A route that asks for the heavy tier is measured against <c>plugin-heavy</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task HeavyTier_RefusesWith429AfterItsBudget()
    {
        using var host = LimitedHost();
        var (_, http) = await OwnerAsync(host, "plugin-limit-heavy");

        await SpendAsync(() => ClientCallAsync(http, "heavy-ping"), 1);
        using var refused = await ClientCallAsync(http, "heavy-ping");

        await AssertRefusedAsync(refused, RateLimitPolicies.PluginHeavy);
    }

    /// <summary>An admin call is counted per administrator, on the tier its route declares.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AdminCalls_AreCountedPerAdministratorOnTheirTier()
    {
        using var host = LimitedHost();
        using var http = await AdminOfAsync(host);

        await SpendAsync(() => AdminCallAsync(http, "stats"), Permits);
        using var defaultRefused = await AdminCallAsync(http, "stats");
        await SpendAsync(() => AdminCallAsync(http, "strict-stats"), 1);
        using var strictRefused = await AdminCallAsync(http, "strict-stats");
        await SpendAsync(() => AdminCallAsync(http, "heavy-stats"), 1);
        using var heavyRefused = await AdminCallAsync(http, "heavy-stats");

        await AssertRefusedAsync(defaultRefused, RateLimitPolicies.PluginDefault);
        await AssertRefusedAsync(strictRefused, RateLimitPolicies.PluginStrict);
        await AssertRefusedAsync(heavyRefused, RateLimitPolicies.PluginHeavy);
    }

    /// <summary>One plugin's spent budget does not refuse the same caller on another plugin, and another caller is not refused on it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheBudget_IsPerPluginAndPerCaller()
    {
        using var host = LimitedHost();
        var (_, spender) = await OwnerAsync(host, "plugin-limit-spender");
        var (_, other) = await OwnerAsync(host, "plugin-limit-bystander");

        await SpendAsync(() => ClientCallAsync(spender, "ping"), Permits);
        using var refused = await ClientCallAsync(spender, "ping");
        using var otherPlugin = await ClientCallAsync(spender, "ping", _fixture.OtherPluginId);
        using var otherCaller = await ClientCallAsync(other, "ping");

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherPlugin.StatusCode.Should().Be(HttpStatusCode.OK);
        otherCaller.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A request that matches no declared route spends no plugin budget and still answers its own 404.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUndeclaredRoute_IsNotMeasuredAndAnswers404()
    {
        using var host = LimitedHost();
        var (_, http) = await OwnerAsync(host, "plugin-limit-undeclared");

        for (var i = 0; i < Permits + 1; i++)
        {
            using var response = await ClientCallAsync(http, "nothing/here");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using var served = await ClientCallAsync(http, "ping");
        served.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A route the caller lacks the permission for is not measured, so a 429 never tells it from an undeclared route: it
    /// keeps answering its own 404, however often it is called.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APermissionlessRoute_IsNotMeasuredAndAnswers404()
    {
        using var host = LimitedHost();
        var (owner, _) = await OwnerAsync(host, "plugin-limit-permissionless");
        var linked = await ClientsTestSeeds.LinkedToAsync(
            _fixture.Factory, "plugin-limit-permissionless-sub", owner.ClientId, ClientPermission.ViewDomains);
        using var sub = AppPluginFixture.WithBearerOf(host, linked);

        for (var i = 0; i < Permits + 1; i++)
        {
            using var response = await ClientCallAsync(sub, "invoices/1");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// A plugin that is switched off is not measured, so a 429 never tells it from an unknown plugin: it keeps answering its
    /// own 404 however often it is called, while a permitted caller on an enabled route is still refused once the budget is spent.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADisabledPlugin_IsNotMeasuredWhileAnEnabledRouteStillIs()
    {
        using var host = LimitedHost();
        var (_, http) = await OwnerAsync(host, "plugin-limit-disabled");

        for (var i = 0; i < Permits + 2; i++)
        {
            using var response = await ClientCallAsync(http, "ping", _fixture.DisabledPluginId);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        await SpendAsync(() => ClientCallAsync(http, "ping"), Permits);
        using var refused = await ClientCallAsync(http, "ping");
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        _fixture.Probe.Constructed.Should().Be(Permits);
    }

    /// <summary>
    /// A streaming route is refused with 429 <c>plugin-stream</c> while the caller holds as many open streams as are allowed,
    /// here one real stream kept open by the plugin, and is served again once that stream has ended.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StreamingRoute_IsRefusedWhileTheCallerHoldsTheAllowedStreams()
    {
        // A large default budget: the retries below wait on the stream allowance, they must not run out the tier first.
        using var host = LimitedHost(defaultPermits: 1000);
        var (_, http) = await OwnerAsync(host, "plugin-limit-stream");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(GatedStream.Open(gate.Task, CancellationToken.None));

        // Headers are in, the plugin has sent its first event and is waiting: the stream is open and holds the allowance.
        using var open = await http.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/api/client/plugins/{_fixture.PluginId}/stream"), HttpCompletionOption.ResponseHeadersRead);
        open.StatusCode.Should().Be(HttpStatusCode.OK);
        using var reader = new StreamReader(await open.Content.ReadAsStreamAsync());
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15))).Should().Be("data: first");

        using var refused = await ClientCallAsync(http, "stream");
        await AssertRefusedAsync(refused, RateLimitPolicies.PluginStream);

        // The stream ends, its response completes and the lease goes with it.
        gate.SetResult();
        (await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(15))).Should().Contain("data: last");
        using var released = await CallOnceReleasedAsync(http, "stream");

        released.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The lease of a streaming call is released when its response ends: the same caller calls the stream route more times
    /// than the allowed number of open streams, holding none, and no call is refused with 429. Without the release at the
    /// end of the response the second call would find the only allowance taken.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StreamingRoute_ReleasesItsLeaseWhenTheResponseEnds()
    {
        using var host = LimitedHost(defaultPermits: 100);
        var (_, http) = await OwnerAsync(host, "plugin-limit-stream-release");
        var calls = Limits(100).PluginStreamPermitLimit + 3;

        _fixture.Probe.Behaviour = _ => PluginReply.Stream(GatedStream.Open(Task.CompletedTask, CancellationToken.None));

        for (var i = 0; i < calls; i++)
        {
            using var response = await CallOnceReleasedAsync(http, "stream");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be("data: first\n\ndata: last\n\n");
        }
    }

    /// <summary>A stream the caller holds does not refuse a route that does not stream, and another caller's streams are their own.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task HeldStreams_DoNotRefuseOrdinaryRoutesOrOtherCallers()
    {
        using var host = LimitedHost();
        var (owner, http) = await OwnerAsync(host, "plugin-limit-stream-holder");
        var (_, other) = await OwnerAsync(host, "plugin-limit-stream-other");
        var limiter = host.Services.GetRequiredService<PluginRateLimiter>();

        using var held = await limiter.AcquireStreamAsync(
            PluginRateLimiter.KeyFor(_fixture.PluginId, $"client:{owner.ClientId}"), CancellationToken.None);

        using var ordinary = await ClientCallAsync(http, "ping");
        using var otherStream = await ClientCallAsync(other, "stream");

        held.IsAcquired.Should().BeTrue();
        ordinary.StatusCode.Should().Be(HttpStatusCode.OK);
        otherStream.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
