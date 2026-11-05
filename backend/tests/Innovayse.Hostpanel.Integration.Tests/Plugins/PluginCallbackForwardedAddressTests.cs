using System.Net;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The public callback door counts a request against the client address the configured forwarded headers resolve to: behind
/// a trusted proxy two senders with different <c>X-Forwarded-For</c> addresses have a budget each, and from a peer that is
/// not a configured proxy the header is ignored, so a sender cannot pick its own bucket by writing it.
/// </summary>
public sealed class PluginCallbackForwardedAddressTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The requests the callback tier allows before it refuses.</summary>
    private const int Permits = 2;

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginCallbackForwardedAddressTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>Gives every request the connection address a real proxy would have, as the test server has none of its own.</summary>
    private sealed class PeerAddressStartupFilter : IStartupFilter
    {
        /// <summary>The address of the peer that connects to the API.</summary>
        private readonly IPAddress _peer;

        /// <summary>Initializes the filter.</summary>
        /// <param name="peer">The address of the peer that connects to the API.</param>
        public PeerAddressStartupFilter(IPAddress peer)
        {
            _peer = peer;
        }

        /// <summary>Puts the peer address on the connection before the rest of the pipeline runs.</summary>
        /// <param name="next">Builds the rest of the pipeline.</param>
        /// <returns>The pipeline with the address step first.</returns>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, step) =>
                {
                    context.Connection.RemoteIpAddress = _peer;
                    await step();
                });
                next(app);
            };
        }
    }

    /// <summary>Derives a host with a tiny callback budget, the given peer address and the given trusted proxies.</summary>
    /// <param name="peer">The address of the peer that connects to the API.</param>
    /// <param name="knownProxy">The one address believed to be a proxy.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    private WebApplicationFactory<Program> Host(IPAddress peer, string knownProxy)
    {
        var limits = new RateLimitOptions
        {
            PluginCallback = new RateLimitTierOptions { PermitLimit = Permits, WindowSeconds = 60 },
            TrustedProxyCount = 1,
            KnownProxies = [knownProxy],
        };

        return _fixture.Derive(services =>
        {
            services.RemoveAll<PluginRateLimiter>();
            services.AddSingleton(new PluginRateLimiter(limits, disabled: false));
            services.Configure<RateLimitOptions>(options =>
            {
                options.TrustedProxyCount = limits.TrustedProxyCount;
                options.KnownProxies = limits.KnownProxies;
            });
            services.AddSingleton<IStartupFilter>(new PeerAddressStartupFilter(peer));
        });
    }

    /// <summary>Posts to the callback door as the given forwarded client.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="forwardedFor">The <c>X-Forwarded-For</c> value.</param>
    /// <returns>The status of the answer.</returns>
    private async Task<HttpStatusCode> PostAsync(HttpClient http, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/plugins/{_fixture.PluginId}/callback/pay")
        {
            Content = new StringContent("{}"),
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        using var response = await http.SendAsync(request);
        return response.StatusCode;
    }

    /// <summary>Behind a trusted proxy, the budget is the forwarded client's own: one client spending it does not refuse another.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task BehindATrustedProxy_TheBudgetIsPerForwardedClientAddress()
    {
        using var host = Host(IPAddress.Parse("10.0.0.5"), "10.0.0.5");
        using var http = host.CreateClient();

        for (var i = 0; i < Permits; i++)
        {
            (await PostAsync(http, "203.0.113.1")).Should().Be(HttpStatusCode.NotFound);
        }

        (await PostAsync(http, "203.0.113.1")).Should().Be(HttpStatusCode.TooManyRequests);
        (await PostAsync(http, "203.0.113.2")).Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>From a peer that is not a configured proxy, the header is ignored and every request shares the peer's budget.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FromAnUntrustedPeer_TheForwardedHeaderIsIgnored()
    {
        using var host = Host(IPAddress.Parse("198.51.100.7"), "10.0.0.5");
        using var http = host.CreateClient();

        for (var i = 0; i < Permits; i++)
        {
            (await PostAsync(http, $"203.0.113.{i + 1}")).Should().Be(HttpStatusCode.NotFound);
        }

        (await PostAsync(http, "203.0.113.99")).Should().Be(HttpStatusCode.TooManyRequests);
    }
}
