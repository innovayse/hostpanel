using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary><c>GET /api/auth/email-verified</c>: whether the caller's credential says their email is confirmed. Local mode only: the sign-on provider branch needs the optional sign-on provider package (INNOVAYSE_SSO), which the standard test build excludes.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetEmailVerifiedTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A signed-in caller gets the answer under the <c>verified</c> key.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetEmailVerifiedAnswersForASignedInCaller()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/email-verified");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("verified", out _).Should().BeTrue();
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetEmailVerifiedRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/email-verified");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
