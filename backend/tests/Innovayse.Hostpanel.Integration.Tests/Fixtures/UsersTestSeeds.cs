using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared seeding, authentication and single sign-on mode helpers for the Admin/Users module's per-use-case
/// Integration tests. Covers both deployment shapes: local mode (this deployment owns its
/// accounts, <c>IUserProvisioning</c> writes for real) and single sign-on mode (every write refuses with its
/// own <c>USER_PROVISIONING_NOT_ALLOWED_*</c> code, which is what <c>SsoModeUserProvisioning</c>
/// answers unconditionally, before any network call). Every <c>Admin/Users/*Tests.cs</c> file
/// calls into this rather than repeating its own copy.
/// </summary>
internal static class UsersTestSeeds
{
    /// <summary>A subject no seeded or created account ever takes.</summary>
    public const string NoSuchSubject = "00000000-0000-0000-0000-000000000000";

    /// <summary>Shared test password for every client user this class registers.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>A client carrying the seeded admin's bearer token, against the shared local-mode host.</summary>
    /// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
    /// <returns>An authenticated client.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Registers a brand-new client user and returns an authenticated <see cref="HttpClient"/>
    /// carrying only the Client role: the caller every admin-only boundary test refuses.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">A short label folded into the unique email, for readable test failures.</param>
    /// <returns>An authenticated client with no elevated role.</returns>
    public static async Task<HttpClient> ClientRoleClientAsync(IntegrationTestFactory factory, string label)
    {
        var httpClient = factory.CreateClient();
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Jamie",
            lastName = "Boundary",
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    /// <summary>Registers a fresh local account and returns its subject and address.</summary>
    /// <param name="client">An unauthenticated client against the local-mode host.</param>
    /// <returns>The new account's subject and the address it registered with.</returns>
    public static async Task<RegisteredAccount> RegisterUserAsync(HttpClient client)
    {
        var email = $"admin-users-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jamie",
            lastName = "Doe",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new RegisteredAccount(json.GetProperty("userId").GetString()!, email);
    }

    /// <summary>Asserts the refusal carries the expected error code.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }

    /// <summary>
    /// Builds a host in single sign-on mode, sharing the local-mode host's PostgreSQL container, with
    /// <see cref="IIdentityProvider"/> replaced by a fake so no test here needs a real sign-on provider to
    /// answer identity reads. <c>IUserProvisioning</c> stays the real <c>SsoModeUserProvisioning</c>
    /// the composition root registers for this mode, which is the refusal under test.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="knownAccount">
    /// The account every subject resolves to; <see langword="null"/> makes every subject unknown.
    /// </param>
    /// <returns>A factory for the single sign-on mode host.</returns>
    public static WebApplicationFactory<Program> SsoModeHost(IntegrationTestFactory factory, IdentityAccount? knownAccount)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:Mode", "sso");
            builder.UseSetting("Auth:AppName", "hostpanel");
            builder.UseSetting("Auth:Authority", "https://sso.invalid");
            builder.UseSetting("Auth:PublicAuthority", "https://sso.invalid");
            builder.UseSetting("Auth:ClientId", "hostpanel");
            builder.UseSetting("Auth:ClientSecret", "test-secret");
            builder.UseSetting("Auth:RedisConnection", "localhost:6379");
            builder.UseSetting("Sso:Authority", "https://sso.invalid");
            builder.UseSetting("Sso:ClientId", "hostpanel");
            builder.UseSetting("Sso:ServiceKey", "test-service-key");

            // Required whenever Auth:Mode is not local (BackchannelLogoutOptionsValidator), so an external sign-on provider host
            // refuses to boot without it.
            builder.UseSetting("BackchannelLogout:Key", "test-backchannel-key");

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IIdentityProvider>(new FakeIdentityProvider(knownAccount));
            });
        });
    }

    /// <summary>
    /// An admin-role client against a single sign-on mode host, authenticated with a locally minted JWT:
    /// the admin panel's own credential in that mode, which the <c>LocalJwt</c> scheme accepts
    /// without contacting the sign-on provider.
    /// </summary>
    /// <param name="host">The single sign-on mode host.</param>
    /// <returns>An authenticated client.</returns>
    public static HttpClient SsoAdminClient(WebApplicationFactory<Program> host)
    {
        var jwt = host.Services.GetRequiredService<IJwtService>();
        var token = jwt.GenerateAccessToken(new AccessTokenClaims(
            UserId: $"admin-{Guid.NewGuid():N}",
            Email: "admin@example.com",
            FirstName: "Admin",
            LastName: "User",
            Roles: [Roles.Admin],
            EmailVerified: true,
            // Read by nothing in this mode: the session version is checked only where this
            // deployment owns the accounts.
            SessionVersion: "single-sign-on-mode"));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
