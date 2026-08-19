namespace Innovayse.Integration.Tests.Auth;

using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Boots the API the way a deployment whose people live in the SSO boots it, and checks
/// which authentication scheme a request is measured against.
/// </summary>
/// <remarks>
/// <para>
/// The rest of this suite runs <c>Auth:Mode=local</c>, deliberately — it creates its users
/// through <c>POST /api/auth/register</c>, which only exists there. The cost of that is
/// that nothing exercised the SSO mode's authentication wiring, and a bug lived in it for
/// five days: registering the platform cookie handler made the cookie the default scheme,
/// so every <c>[Authorize(Roles = …)]</c> endpoint — 85 of them — stopped reading the
/// Authorization header. `[Authorize]` on its own kept working, because it uses
/// DefaultPolicy and that names all three schemes; naming a role makes MVC build its own
/// policy, which carries none and falls back to the default. The client portal calls this
/// API with a bearer token, so its dashboard rendered empty against a 401 that said
/// nothing.
/// </para>
/// <para>
/// These two tests are the cheapest thing that would have caught it. They assert the
/// decision rather than a response, so they need no SSO to talk to.
/// </para>
/// </remarks>
public sealed class SsoModeAuthenticationSchemeTests : IAsyncLifetime
{
    private const string SmartAuthScheme = "InnovayseSmart";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new SsoModeFactory(_postgres.GetConnectionString());
        // Force the host to build. Nothing is requested over HTTP: what is under test is
        // how the container was composed, not how it answers.
        _ = _factory.Services;
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public void TheDefaultSchemeIsTheSelector_NotTheCookie()
    {
        var options = _factory!.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        options.DefaultAuthenticateScheme.Should().Be(SmartAuthScheme);
        options.DefaultChallengeScheme.Should().Be(SmartAuthScheme);
    }

    [Theory]
    [InlineData("Bearer eyJhbGciOiJSUzI1NiJ9.e30.x", JwtBearerDefaults.AuthenticationScheme)]
    [InlineData(null, Innovayse.Auth.CookieSessionHandler.SchemeName)]
    [InlineData("", Innovayse.Auth.CookieSessionHandler.SchemeName)]
    [InlineData("Basic dXNlcjpwYXNz", Innovayse.Auth.CookieSessionHandler.SchemeName)]
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
        if (authorization is not null) context.Request.Headers.Authorization = authorization;

        options.ForwardDefaultSelector!(context).Should().Be(expected);
    }

    /// <summary>Boots Program.cs with the settings an SSO-owned deployment supplies.</summary>
    private sealed class SsoModeFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // UseSetting rather than ConfigureAppConfiguration, for the reason spelled out
            // in IntegrationTestFactory: Program.cs is top-level code and reads these while
            // composing the builder, before ConfigureAppConfiguration's sources exist.
            builder.UseSetting("Auth:Mode", "sso");
            // Base64 of 32 bytes, not 32 characters — AddInfrastructure decodes it and
            // refuses anything that is not exactly a 256-bit key. Same value the rest of
            // the suite uses.
            builder.UseSetting("EncryptionKey", "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");

            // AddInnovayseAuth validates that each of these is present and refuses to start
            // naming the one that is missing. None is contacted while the host is built —
            // the bearer handler fetches discovery lazily, on the first token it is asked
            // to validate, and no test here presents one.
            builder.UseSetting("Auth:AppName", "hostpanel");
            builder.UseSetting("Auth:Authority", "https://sso.invalid");
            builder.UseSetting("Auth:PublicAuthority", "https://sso.invalid");
            builder.UseSetting("Auth:ClientId", "hostpanel");
            builder.UseSetting("Auth:ClientSecret", "test-secret");
            builder.UseSetting("Auth:RedisConnection", "localhost:6379");
            builder.UseSetting("Sso:Authority", "https://sso.invalid");
            builder.UseSetting("Sso:ClientId", "hostpanel");
            builder.UseSetting("Sso:ServiceKey", "test-service-key");

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                }));
        }
    }
}
