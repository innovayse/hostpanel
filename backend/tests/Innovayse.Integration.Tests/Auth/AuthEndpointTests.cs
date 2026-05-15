namespace Innovayse.Integration.Tests.Auth;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

/// <summary>Integration tests for POST /api/auth/* endpoints.</summary>
public sealed class AuthEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Register with valid data returns 200 with access token.</summary>
    [Fact]
    public async Task Register_WithValidData_Returns200WithAccessTokenAsync()
    {
        var client = factory.CreateClient();
        var body = new
        {
            email = $"test-{Guid.NewGuid()}@example.com",
            password = "Password123!",
            firstName = "John",
            lastName = "Doe"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("accessToken");
    }

    /// <summary>Login with valid credentials returns 200 with access token.</summary>
    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithAccessTokenAsync()
    {
        var client = factory.CreateClient();
        var email = $"login-test-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jane",
            lastName = "Doe"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("accessToken");
    }

    /// <summary>Login with wrong password returns non-200 status.</summary>
    [Fact]
    public async Task Login_WithWrongPassword_ReturnsErrorAsync()
    {
        var client = factory.CreateClient();
        var email = $"wrong-pwd-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jane",
            lastName = "Doe"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword!"
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    /// <summary>Logout returns 204 No Content.</summary>
    [Fact]
    public async Task Logout_Returns204Async()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync("/api/auth/logout", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
