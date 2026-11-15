using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared sign-in helpers for the Integration tests: the admin client of any host derived from the shared one (a clocked host
/// included), which every module's tests take from here instead of keeping a copy, and, for the Auth module's per-use-case tests,
/// registering and signing in a brand-new local account and enrolling its authenticator app; the codes themselves come from
/// <see cref="TwoFactorHost"/>. Every <c>Auth/*Tests.cs</c> file covering two-factor calls into this rather than repeating its own copy.
/// </summary>
internal static class AuthTestSeeds
{
    /// <summary>The password every account <see cref="NewAccountClientAsync"/> registers signs in with.</summary>
    public const string AccountPassword = "Password123!";

    /// <summary>Makes a client of a host derived from the shared one, carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host, which mints the admin token.</param>
    /// <param name="host">The host the client talks to: the shared host itself or one derived from it.</param>
    /// <returns>An authenticated admin <see cref="HttpClient"/> of <paramref name="host"/>.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory, WebApplicationFactory<Program> host)
    {
        return await SignedInAsAdminAsync(factory, host.CreateClient());
    }

    /// <summary>Makes a client of a clocked host, carrying the seeded admin's bearer token.</summary>
    /// <remarks>
    /// The token is minted by the shared host, which signs and checks it against the real time. The check is on the token's
    /// expiry only, so a clocked host standing at another moment accepts it all the same.
    /// </remarks>
    /// <param name="factory">The shared API + PostgreSQL host, which mints the admin token.</param>
    /// <param name="host">The clocked host the client talks to.</param>
    /// <returns>An authenticated admin <see cref="HttpClient"/> of <paramref name="host"/>.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory, ClockedHost host)
    {
        return await SignedInAsAdminAsync(factory, host.CreateClient());
    }

    /// <summary>Registers and signs in a brand-new local account, and returns an authenticated client.</summary>
    /// <param name="client">A fresh, unauthenticated client of the host the account is created on.</param>
    /// <returns>A client carrying the new account's bearer token.</returns>
    public static async Task<HttpClient> NewAccountClientAsync(HttpClient client)
    {
        var email = $"twofactor-{Guid.NewGuid():N}@example.com";
        const string password = AccountPassword;

        (await client.PostAsJsonAsync("/api/auth/register",
            new { email, password, firstName = "Two", lastName = "Factor" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loggedIn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("accessToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Puts the seeded admin's bearer token on a client.</summary>
    /// <param name="factory">The shared API + PostgreSQL host, which mints the admin token.</param>
    /// <param name="client">A fresh, unauthenticated client of the host under test.</param>
    /// <returns><paramref name="client"/>, authenticated.</returns>
    private static async Task<HttpClient> SignedInAsAdminAsync(IntegrationTestFactory factory, HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return client;
    }

    /// <summary>Starts enrolment and returns the issued secret.</summary>
    /// <param name="client">The authenticated account's client.</param>
    /// <returns>The base32 TOTP secret.</returns>
    public static async Task<string> StartSetupAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/me/2fa/setup", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("secret").GetString()!;
    }
}
