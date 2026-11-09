using Innovayse.Hostpanel.Shared.Middleware;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// <see cref="TraceIdMiddleware"/>, wired into <c>Program.cs</c>: every response carries our own
/// <c>X-Request-Id</c>, never one a caller sent in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TraceIdMiddlewareTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every response, success or refusal, carries a trace id distinct from any the caller sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EveryResponse_CarriesItsOwnRequestIdAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TraceIdMiddleware.HeaderName, "caller-supplied-id");

        var response = await client.GetAsync("/health");

        response.Headers.TryGetValues(TraceIdMiddleware.HeaderName, out var values).Should().BeTrue();
        var traceId = values!.Single();
        traceId.Should().NotBe("caller-supplied-id");
        Guid.TryParseExact(traceId, "N", out _).Should().BeTrue();
    }

    /// <summary>Two requests get two different trace ids.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoRequests_GetTwoDifferentRequestIdsAsync()
    {
        var client = factory.CreateClient();

        var first = await client.GetAsync("/health");
        var second = await client.GetAsync("/health");

        first.Headers.GetValues(TraceIdMiddleware.HeaderName).Single()
            .Should().NotBe(second.Headers.GetValues(TraceIdMiddleware.HeaderName).Single());
    }
}
