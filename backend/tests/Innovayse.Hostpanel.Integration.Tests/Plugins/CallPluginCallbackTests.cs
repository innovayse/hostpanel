using System.Net;
using System.Text;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The public plugin callback door (<c>/api/plugins/{id}/callback/{path}</c>), against Postgres and the test-only plugins of
/// <see cref="AppPluginFixture"/>, with no sign-in anywhere. These tests use plugins that declare no
/// <c>api.callbacks</c>: the door is closed for them, so every request is "no such route", the answer is one and the
/// same for every reason, a plugin's client and admin routes are not reachable through it, an oversized body is refused
/// before it is read, and the callback rate limit counts every request. The tests of a declared callback reaching the
/// plugin are in <c>CallPluginCallbackDeclaredTests</c>.
/// </summary>
public sealed class CallPluginCallbackTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The requests the callback tier allows in the rate limit test.</summary>
    private const int Permits = 2;

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>The log position at the start of this test.</summary>
    private readonly int _logMark;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes, and marks the log.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public CallPluginCallbackTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
        _logMark = _fixture.LogMark();
    }

    /// <summary>The raw answer of a callback: the status, the content type and the body exactly as sent.</summary>
    /// <param name="Status">The status code.</param>
    /// <param name="ContentType">The Content-Type header, parameters included.</param>
    /// <param name="Body">The raw response body.</param>
    private sealed record Answer(HttpStatusCode Status, string? ContentType, string Body);

    /// <summary>Sends an anonymous request to a callback route and keeps the raw answer.</summary>
    /// <param name="http">The caller, which carries no credentials.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The path below the plugin's callback prefix.</param>
    /// <param name="body">The raw body, or <see langword="null"/> for none.</param>
    /// <returns>The answer.</returns>
    private static async Task<Answer> AnswerOfAsync(HttpClient http, HttpMethod method, string pluginId, string path, string? body = null)
    {
        using var request = new HttpRequestMessage(method, $"/api/plugins/{pluginId}/callback/{path}");
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await http.SendAsync(request);
        return new Answer(
            response.StatusCode, response.Content.Headers.ContentType?.ToString(), await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// With no callback declared, an unknown plugin, a disabled one, an undeclared path, a wrong method and a route the
    /// plugin declares only for a signed-in client or administrator all answer the same 404, byte for byte, with no sign-in
    /// asked for, and the plugin is never built.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EveryRefusalReason_AnswersTheSameBytes_WithoutSignIn()
    {
        using var http = _fixture.Host.CreateClient();

        var unknown = await AnswerOfAsync(http, HttpMethod.Post, "no-such-plugin", "pay");
        var disabled = await AnswerOfAsync(http, HttpMethod.Post, _fixture.DisabledPluginId, "pay");
        var undeclared = await AnswerOfAsync(http, HttpMethod.Post, _fixture.PluginId, "nothing/here");
        var wrongMethod = await AnswerOfAsync(http, HttpMethod.Delete, _fixture.PluginId, "pay");
        var clientRoute = await AnswerOfAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");
        var adminRoute = await AnswerOfAsync(http, HttpMethod.Get, _fixture.PluginId, "stats");
        var notAnAppPlugin = await AnswerOfAsync(http, HttpMethod.Post, _fixture.NotAppPluginId, "pay");

        unknown.Status.Should().Be(HttpStatusCode.NotFound);
        unknown.ContentType.Should().NotBeNullOrEmpty();
        unknown.Body.Should().Contain(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        foreach (var refusal in new[] { disabled, undeclared, wrongMethod, clientRoute, adminRoute, notAnAppPlugin })
        {
            refusal.Status.Should().Be(unknown.Status);
            refusal.ContentType.Should().Be(unknown.ContentType);
            refusal.Body.Should().Be(unknown.Body);
        }

        _fixture.Probe.Constructed.Should().Be(0);
        _fixture.Probe.Calls.Should().BeEmpty();
    }

    /// <summary>
    /// A body over the cap sent to a route that is not served is refused as an absent route, not as 413 and not by reading
    /// it, so the status does not tell a declared callback from an undeclared one.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnOversizedBody_ToARouteThatIsNotServed_IsStillTheSame404()
    {
        using var http = _fixture.Host.CreateClient();
        var oversized = new string('x', AppPluginFixture.MaxBodyBytes * 4);

        var big = await AnswerOfAsync(http, HttpMethod.Post, _fixture.PluginId, "pay", oversized);
        var small = await AnswerOfAsync(http, HttpMethod.Post, _fixture.PluginId, "pay", "{}");

        big.Status.Should().Be(HttpStatusCode.NotFound);
        big.Body.Should().Be(small.Body);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>The routes of a plugin's signed-in doors do not answer an anonymous request even though the callback door is public.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheSignedInDoors_StayClosedToAnonymousCallers()
    {
        using var http = _fixture.Host.CreateClient();

        using var client = await http.GetAsync($"/api/client/plugins/{_fixture.PluginId}/ping");
        using var admin = await http.GetAsync($"/api/admin/plugins/{_fixture.PluginId}/ext/stats");

        client.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        admin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The callback tier refuses with the standard 429 once one address has spent its budget, and it counts every request,
    /// an unknown plugin included, so the refusal says nothing about what is installed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheCallbackTier_RefusesWith429AfterItsBudget_WhateverThePlugin()
    {
        using var host = LimitedHost();
        using var http = host.CreateClient();

        for (var i = 0; i < Permits; i++)
        {
            using var served = await http.PostAsync($"/api/plugins/no-such-plugin-{i}/callback/pay", new StringContent("{}"));
            served.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using var refused = await http.PostAsync($"/api/plugins/{_fixture.PluginId}/callback/pay", new StringContent("{}"));

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        refused.Headers.RetryAfter.Should().NotBeNull();
        refused.Headers.GetValues("X-RateLimit-Remaining").Should().ContainSingle().Which.Should().Be("0");
        _fixture.LoggedRateLimitRefusal(RateLimitPolicies.PluginCallback, "callback", _logMark).Should().BeTrue();
        await ClientsTestSeeds.AssertCodeAsync(refused, SharedErrorCodes.RateLimited);
    }

    /// <summary>The callback budget is its own: spending it leaves the signed-in plugin tiers untouched.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheCallbackBudget_IsNotTheSignedInDoorsBudget()
    {
        using var host = LimitedHost();
        using var http = host.CreateClient();

        for (var i = 0; i <= Permits; i++)
        {
            using var spent = await http.PostAsync($"/api/plugins/{_fixture.PluginId}/callback/pay", new StringContent("{}"));
        }

        using var client = await http.GetAsync($"/api/client/plugins/{_fixture.PluginId}/ping");

        client.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Derives a host whose plugin limiter is switched on with a tiny callback budget.</summary>
    /// <returns>The host, to dispose when the test ends.</returns>
    private WebApplicationFactory<Program> LimitedHost()
    {
        var limits = new RateLimitOptions
        {
            PluginCallback = new RateLimitTierOptions { PermitLimit = Permits, WindowSeconds = 60 },
        };

        return _fixture.Derive(services =>
        {
            services.RemoveAll<PluginRateLimiter>();
            services.AddSingleton(new PluginRateLimiter(limits, disabled: false));
        });
    }
}
