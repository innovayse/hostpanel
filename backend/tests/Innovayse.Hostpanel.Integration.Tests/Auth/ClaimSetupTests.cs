using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <c>POST /api/auth/setup</c> through the endpoint's own use case: the refusal paths and the
/// optional body. The grant itself is covered by <c>CompleteSetupTests</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClaimSetupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The claim is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClaimSetupRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/setup", new { setupToken = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A signed-in caller presenting a wrong token, or no body at all, is refused with
    /// <c>SETUP_TOKEN_INVALID</c> while setup is open or <c>SETUP_ALREADY_COMPLETED</c> once
    /// somebody holds Admin: an empty body reaches the handler rather than failing model binding.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClaimSetupWithAWrongTokenIsRefused()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var withToken = await client.PostAsJsonAsync("/api/auth/setup", new { setupToken = "not-the-token" });
        var withoutBody = await client.PostAsync("/api/auth/setup", null);

        foreach (var response in new[] { withToken, withoutBody })
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Conflict);
            (await ClientsTestSeeds.CodeOfAsync(response)).Should()
                .BeOneOf("SETUP_TOKEN_INVALID", "SETUP_ALREADY_COMPLETED");
        }
    }
}
