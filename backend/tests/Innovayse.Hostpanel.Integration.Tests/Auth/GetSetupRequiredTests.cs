using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary><c>GET /api/auth/setup-required</c>: whether initial admin setup is outstanding, readable before authenticating.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSetupRequiredTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An anonymous caller reads whether setup is outstanding, and the answer carries nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetSetupRequiredAnswersAnonymously()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/setup-required");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("required").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
        body.TryGetProperty("tokenRequired", out _).Should().BeFalse("setup is token-gated in every mode, so no flag says so");
    }
}
