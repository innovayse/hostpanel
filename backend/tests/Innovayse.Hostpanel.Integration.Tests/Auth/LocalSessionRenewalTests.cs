using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// Sliding renewal of the local session: a token in the last third of its life is re-issued, an
/// expired one is refused, no renewal ever outlives the absolute maximum session age, and a token
/// whose session version the account has moved past is neither accepted nor renewed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LocalSessionRenewalTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A route any signed-in caller may read.</summary>
    private const string Route = "/api/auth/email-verified";

    /// <summary>Access-token length the test hosts run with, in minutes.</summary>
    private const int AccessMinutes = 15;

    /// <summary>A token in the last third of its life is renewed into a later expiry.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TokenNearExpiry_IsRenewedInTheCookie()
    {
        var (client, clock, jwt, services) = Host(maxSessionMinutes: 720);
        var token = await MintAsync(client, jwt, services);
        clock.Advance(TimeSpan.FromMinutes(12));

        var response = await GetWithCookieAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = RenewedCookie(response);
        renewed.Should().NotBeNull();
        new JwtSecurityTokenHandler().ReadJwtToken(renewed).ValidTo
            .Should().BeAfter(new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo);
    }

    /// <summary>A token with most of its life left is left alone.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TokenEarlyInItsLife_IsNotRenewed()
    {
        var (client, clock, jwt, services) = Host(maxSessionMinutes: 720);
        var token = await MintAsync(client, jwt, services);
        clock.Advance(TimeSpan.FromMinutes(2));

        var response = await GetWithCookieAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        RenewedCookie(response).Should().BeNull();
    }

    /// <summary>An expired token is refused and nothing is issued in its place.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ExpiredToken_IsRefusedAndNotRenewed()
    {
        var (client, clock, jwt, services) = Host(maxSessionMinutes: 720);
        clock.Advance(TimeSpan.FromMinutes(-30));
        var token = await MintAsync(client, jwt, services);
        clock.Advance(TimeSpan.FromMinutes(30));

        var response = await GetWithCookieAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        RenewedCookie(response).Should().BeNull();
    }

    /// <summary>A renewal is capped at the absolute maximum session age.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Renewal_NeverOutlivesTheMaximumSessionAge()
    {
        var (client, clock, jwt, services) = Host(maxSessionMinutes: AccessMinutes);
        var start = clock.GetUtcNow();
        var token = await MintAsync(client, jwt, services);
        clock.Advance(TimeSpan.FromMinutes(12));

        var response = await GetWithCookieAsync(client, token);

        var renewed = RenewedCookie(response);
        renewed.Should().NotBeNull();
        new DateTimeOffset(new JwtSecurityTokenHandler().ReadJwtToken(renewed).ValidTo, TimeSpan.Zero)
            .Should().BeOnOrBefore(start.AddMinutes(AccessMinutes));
    }

    /// <summary>A session past its maximum age is not renewed again.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SessionPastTheMaximumAge_IsNotRenewed()
    {
        var (client, clock, jwt, services) = Host(maxSessionMinutes: AccessMinutes);
        var token = await MintAsync(client, jwt, services);
        clock.Advance(TimeSpan.FromMinutes(AccessMinutes + 1));

        var response = await GetWithCookieAsync(client, token);

        RenewedCookie(response).Should().BeNull();
    }

    /// <summary>A bearer caller (the client portal server) gets the renewed token in a header.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task BearerCaller_GetsRenewedTokenInTheHeader()
    {
        var (client, clock, jwt, services) = Host(maxSessionMinutes: 720);
        var token = await MintAsync(client, jwt, services);
        clock.Advance(TimeSpan.FromMinutes(12));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(LocalSessionRenewalMiddleware.RenewedTokenHeader).Should().ContainSingle();
    }

    /// <summary>
    /// Once the account's session version moves on (a password reset, two-factor switched off, an
    /// address change), a token issued before is refused even while due for renewal, and nothing is
    /// issued in its place.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TokenFromBeforeTheSessionVersionMoved_IsRefusedAndNotRenewed()
    {
        var (client, clock, jwt, services) = Host(maxSessionMinutes: 720);
        var token = await MintAsync(client, jwt, services);
        clock.Advance(TimeSpan.FromMinutes(12));
        using (var scope = services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var subject = new JwtSecurityTokenHandler().ReadJwtToken(token).Subject;
            await users.UpdateSecurityStampAsync((await users.FindByIdAsync(subject))!);
        }

        var response = await GetWithCookieAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        RenewedCookie(response).Should().BeNull();
    }

    /// <summary>A token carrying a session version the account never had is refused.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TokenWithAStaleSessionVersion_IsRefused()
    {
        var (client, _, jwt, _) = Host(maxSessionMinutes: 720);
        var account = await RegisterAsync(client);
        var token = jwt.GenerateAccessToken(new AccessTokenClaims(
            account.UserId, account.Email, FirstName: null, LastName: null, [Roles.Client], EmailVerified: true,
            SessionVersion: "not-the-current-version"));

        var response = await GetWithCookieAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Builds a host with a hand-moved clock and the given maximum session age.</summary>
    /// <param name="maxSessionMinutes">Absolute maximum session age, in minutes.</param>
    /// <returns>The client, the clock and the host token service.</returns>
    private SessionRenewalHost Host(int maxSessionMinutes)
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Session:AccessTokenMinutes", AccessMinutes.ToString());
            builder.UseSetting("Session:MaxSessionMinutes", maxSessionMinutes.ToString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });

        return new(host.CreateClient(), clock, host.Services.GetRequiredService<IJwtService>(), host.Services);
    }

    /// <summary>
    /// Registers a fresh account and mints a token for it at the clock's current instant, carrying
    /// the account's current session version, as a sign-in would.
    /// </summary>
    /// <param name="client">An unauthenticated client of the host.</param>
    /// <param name="jwt">The host token service.</param>
    /// <param name="services">The host's services.</param>
    /// <returns>The token.</returns>
    private static async Task<string> MintAsync(HttpClient client, IJwtService jwt, IServiceProvider services)
    {
        var account = await RegisterAsync(client);
        using var scope = services.CreateScope();
        var version = await scope.ServiceProvider.GetRequiredService<IUserService>()
            .GetSessionVersionAsync(account.UserId, CancellationToken.None);

        return jwt.GenerateAccessToken(new AccessTokenClaims(
            account.UserId, account.Email, FirstName: null, LastName: null, [Roles.Client], EmailVerified: true,
            SessionVersion: version!));
    }

    /// <summary>Registers a fresh local account through the public endpoint.</summary>
    /// <param name="client">An unauthenticated client of the host.</param>
    /// <returns>The new account's subject and address.</returns>
    private static async Task<RegisteredAccount> RegisterAsync(HttpClient client)
    {
        var email = $"renewal-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = "Password123!", firstName = "Renewal", lastName = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new RegisteredAccount(body.GetProperty("userId").GetString()!, email);
    }

    /// <summary>Calls the route presenting the token as the session cookie.</summary>
    /// <param name="client">The host client.</param>
    /// <param name="token">The token to present.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> GetWithCookieAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add("Cookie", $"{LocalSessionCookie.Name}={token}");
        return client.SendAsync(request);
    }

    /// <summary>Reads the session cookie value the response set, if any.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The cookie token, or <see langword="null"/> when none was set.</returns>
    private static string? RenewedCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var prefix = $"{LocalSessionCookie.Name}=";
        var line = cookies.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.Ordinal));
        return line?[prefix.Length..].Split(';')[0];
    }
}
