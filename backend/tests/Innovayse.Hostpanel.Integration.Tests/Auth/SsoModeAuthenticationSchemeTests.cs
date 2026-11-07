#if INNOVAYSE_SSO
// Only compiled when the optional sign-on package is referenced (the InnovayseSso property in
// backend/Directory.Build.props): these tests exercise that package's scheme wiring.
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;
/// <summary>
/// Boots the API the way a deployment whose people live in the sign-on provider boots it, and checks
/// which authentication scheme a request is measured against.
/// </summary>
/// <remarks>
/// <para>
/// The rest of this suite runs <c>Auth:Mode=local</c>, because it creates its users through
/// <c>POST /api/auth/register</c>, which only exists there; these tests cover the single sign-on
/// mode's authentication wiring. Registering the cookie handler must not make the cookie the
/// default scheme: if it did, every endpoint that names a role in <c>[Authorize(Roles = "Admin")]</c>
/// would stop reading the Authorization header. <c>[Authorize]</c> on its own would keep working,
/// because it uses DefaultPolicy and that names all three schemes; naming a role makes MVC build
/// its own policy, which carries none and falls back to the default. The client portal calls
/// this API with a bearer token, so its dashboard would render empty against a silent 401.
/// </para>
/// <para>
/// The tests assert the scheme decision rather than a response, so they need no external
/// sign-on provider to talk to.
/// </para>
/// </remarks>
public sealed class SsoModeAuthenticationSchemeTests : IAsyncLifetime
{
    /// <summary>Name of the policy scheme that selects between the bearer and cookie handlers.</summary>
    private const string SmartAuthScheme = "InnovayseSmart";

    /// <summary>Throw-away database the host under test connects to.</summary>
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    /// <summary>Host booted in single sign-on mode; null until <see cref="InitializeAsync"/> has run.</summary>
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Starts the database container and builds the host in single sign-on mode.</summary>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new SsoModeFactory(_postgres.GetConnectionString());
        // Force the host to build. The scheme tests request nothing over HTTP: what they test is
        // how the container was composed, not how it answers.
        _ = _factory.Services;
    }

    /// <summary>Dispose.</summary>
    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// A host in single sign-on mode without the provider's issuer refuses to start, naming the setting: without it the
    /// provider's back-channel logout would be refused on every call while the host looked healthy.
    /// </summary>
    [Fact]
    public void StartupIsRefused_WhenTheProviderIssuerIsNotConfigured()
    {
        using var factory = new SsoModeFactory(_postgres.GetConnectionString(), bearerIssuer: null);

        var act = () => factory.Services;

        act.Should().Throw<Exception>().Which.ToString().Should().Contain("Auth:BearerIssuer");
    }

    /// <summary>The default scheme is the selector, not the cookie.</summary>
    [Fact]
    public void TheDefaultSchemeIsTheSelector_NotTheCookie()
    {
        var options = _factory!.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        options.DefaultAuthenticateScheme.Should().Be(SmartAuthScheme);
        options.DefaultChallengeScheme.Should().Be(SmartAuthScheme);
    }

    /// <summary>The selector sends abearer to the bearer handler and everything else to the cookie.</summary>
    /// <param name="authorization">The Authorization header value, or null for none.</param>
    /// <param name="expected">The scheme the selector should forward to.</param>
    [Theory]
    [InlineData("Bearer eyJhbGciOiJSUzI1NiJ9.e30.x", JwtBearerDefaults.AuthenticationScheme)]
    [InlineData(null, Innovayse.Auth.Authentication.CookieSessionHandler.SchemeName)]
    [InlineData("", Innovayse.Auth.Authentication.CookieSessionHandler.SchemeName)]
    [InlineData("Basic dXNlcjpwYXNz", Innovayse.Auth.Authentication.CookieSessionHandler.SchemeName)]
    public async Task TheSelectorSendsABearerToTheBearerHandlerAndEverythingElseToTheCookie(
        string? authorization, string expected)
    {
        var provider = _factory!.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await provider.GetSchemeAsync(SmartAuthScheme);
        scheme.Should().NotBeNull("the selector scheme has to exist for anything below to mean anything");

        var options = _factory.Services
            .GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>()
            .Get(SmartAuthScheme);

        var context = new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        options.ForwardDefaultSelector!(context).Should().Be(expected);
    }

    /// <summary>
    /// Where the sign-on provider owns the people the local sign-in route is absent, and says so in the standard
    /// error body (problem+json, the not-found code in <c>type</c>) rather than an empty 404.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task LocalLogin_InSsoMode_RefusesWithTheStandardNotFoundBody()
    {
        using var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "a@example.com", password = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.NotFound);
        body.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }
}

#endif
