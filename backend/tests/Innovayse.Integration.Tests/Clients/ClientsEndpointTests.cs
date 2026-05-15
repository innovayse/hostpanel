namespace Innovayse.Integration.Tests.Clients;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

/// <summary>Integration tests for /api/clients and /api/me endpoints.</summary>
public sealed class ClientsEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Registers a user, then GET /api/me as that client returns 200 with the profile.
    /// Registration triggers async client creation via Wolverine — uses a retry loop
    /// to handle in-process handler latency.
    /// </summary>
    [Fact]
    public async Task GetMyProfile_AfterRegister_Returns200WithProfileAsync()
    {
        var client = factory.CreateClient();
        var email = $"profile-{Guid.NewGuid()}@example.com";

        // Register (triggers CreateClientOnRegisterHandler via Wolverine)
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Alice",
            lastName = "Smith"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authJson!.AccessToken);

        // Retry loop to allow Wolverine in-process handler to complete
        HttpResponseMessage? profileResponse = null;
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(200);
            profileResponse = await client.GetAsync("/api/me");
            if (profileResponse.StatusCode == HttpStatusCode.OK)
            {
                break;
            }
        }

        profileResponse!.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await profileResponse.Content.ReadAsStringAsync();
        json.Should().Contain("Alice");
        json.Should().Contain("Smith");
    }

    /// <summary>Unauthenticated request to /api/me returns 401.</summary>
    [Fact]
    public async Task GetMyProfile_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Admin GET /api/clients returns 401 for unauthenticated request.</summary>
    [Fact]
    public async Task ListClients_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/clients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Helper record to deserialize auth response.</summary>
    /// <param name="AccessToken">The JWT access token.</param>
    /// <param name="ExpiresAt">Token expiry.</param>
    /// <param name="Role">User role.</param>
    private record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role);
}
