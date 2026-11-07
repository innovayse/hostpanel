using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Security;

/// <summary>
/// <c>ProviderLeakGuardMiddleware</c> end to end: a controller that decorates its response with
/// every vendor header must reach the client with none of them, the allow-listed headers of our
/// own file results must survive, and an unhandled provider fault must not name the vendor.
/// </summary>
/// <param name="factory">The shared test host.</param>
public sealed class LeakGuardTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a client against a host that also serves <see cref="LeakProbeController"/>.</summary>
    /// <returns>An anonymous client.</returns>
    private HttpClient ProbeClient()
    {
        return factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(LeakProbeController).Assembly)))
            .CreateClient();
    }

    /// <summary>No vendor header set by the action arrives at the client.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task VendorHeaders_AreStrippedAsync()
    {
        var response = await ProbeClient().GetAsync("/api/leak-probe/download");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var received = response.Headers.Select(h => h.Key).Concat(response.Content.Headers.Select(h => h.Key)).ToList();
        foreach (var name in LeakProbeController.VendorHeaders.Keys)
        {
            received.Should().NotContain(name, because: "the guard strips every non-allow-listed header");
        }
    }

    /// <summary>Limit headers on a response that is not a 429 are removed: they were copied from somewhere else.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RateLimitHeaders_OnASuccess_AreStrippedAsync()
    {
        var response = await ProbeClient().GetAsync("/api/leak-probe/download");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var name in LeakProbeController.RateLimitHeaders.Keys)
        {
            response.Headers.Select(h => h.Key).Should().NotContain(name, because: "only a 429 may carry the limiter's headers");
        }
    }

    /// <summary>Limit headers on a 429 survive, and the vendor headers next to them do not.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RateLimitHeaders_OnA429_SurviveAsync()
    {
        var response = await ProbeClient().GetAsync("/api/leak-probe/refused");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        foreach (var (name, value) in LeakProbeController.RateLimitHeaders)
        {
            response.Headers.GetValues(name).Should().ContainSingle().Which.Should().Be(value);
        }

        foreach (var name in LeakProbeController.VendorHeaders.Keys)
        {
            response.Headers.Select(h => h.Key).Should().NotContain(name);
        }
    }

    /// <summary>No response names the web server: there is no <c>Server</c> header.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServerHeader_IsNotSentAsync()
    {
        var response = await ProbeClient().GetAsync("/api/leak-probe/download");

        response.Headers.Contains("Server").Should().BeFalse(because: "the web server must not name itself");
    }

    /// <summary>Content-Type, Content-Disposition and ETag of our own download survive the header filtering.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AllowListedHeaders_SurviveAsync()
    {
        var response = await ProbeClient().GetAsync("/api/leak-probe/download");

        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition?.FileName.Should().Be("\"export.csv\"");
        response.Headers.ETag?.Tag.Should().Be("\"v1\"");
    }

    /// <summary>A provider fault answers with headers stripped and a body that never names the vendor.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ProviderFault_NeitherLeaksHeadersNorVendorNameAsync()
    {
        var response = await ProbeClient().GetAsync("/api/leak-probe/fault");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        foreach (var name in LeakProbeController.VendorHeaders.Keys)
        {
            response.Headers.Select(h => h.Key).Should().NotContain(name);
        }

        body.Should().NotContainEquivalentOf(LeakProbeController.VendorName);
        body.Should().NotContainEquivalentOf("registrar.example");
        body.Should().NotContain("2019166");
    }
}
