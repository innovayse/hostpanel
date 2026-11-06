using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>Boots Program.cs with the settings a provider-owned deployment supplies.</summary>
/// <param name="connectionString">The test database the host connects to.</param>
/// <param name="redisConnection">
/// The Redis the sign-on client package keeps sessions and its key ring in. The default is an address nothing
/// listens on, which is enough for tests that never reach a session or protect a cookie.
/// </param>
/// <param name="bearerIssuer">
/// The issuer the provider stamps into its tokens (<c>Auth:BearerIssuer</c>). Null leaves it unset, which a host
/// in single sign-on mode refuses to start with.
/// </param>
internal sealed class SsoModeFactory(
    string connectionString,
    string redisConnection = "localhost:6379",
    string? bearerIssuer = "https://sso.invalid/") : WebApplicationFactory<Program>
{
    /// <summary>
    /// Lets the API's entry point start the host under the test server, as <see cref="IntegrationTestFactory"/>
    /// does. <c>Program</c> ends in <c>RunJasperFxCommands</c>, which otherwise prints its usage and never
    /// starts the host: the container is still built, but no handler chain is compiled and every request
    /// fails with "the server has not been started". Set here too so a run that touches only this factory
    /// does not depend on another fixture having been loaded first.
    /// </summary>
    static SsoModeFactory()
    {
        JasperFxEnvironment.AutoStartHost = true;
    }

    /// <summary>Boots the host in the Testing environment, in single sign-on mode, against the test database.</summary>
    /// <param name="builder">The web host being configured.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting rather than ConfigureAppConfiguration, for the reason spelled out
        // in IntegrationTestFactory: Program.cs is top-level code and reads these while
        // composing the builder, before ConfigureAppConfiguration's sources exist.
        builder.UseSetting("Auth:Mode", "sso");
        // Base64 of 32 bytes, not 32 characters: AddInfrastructure decodes it and
        // refuses anything that is not exactly a 256-bit key. Same value the rest of
        // the suite uses.
        builder.UseSetting("EncryptionKey", "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");

        // The sign-on package's registration validates that each of these is present and refuses to start
        // naming the one that is missing. None is contacted while the host is built:
        // the bearer handler fetches discovery lazily, on the first token it is asked
        // to validate, and no test here presents one.
        builder.UseSetting("Auth:AppName", "hostpanel");
        builder.UseSetting("Auth:Authority", "https://sso.invalid");
        builder.UseSetting("Auth:PublicAuthority", "https://sso.invalid");
        builder.UseSetting("Auth:ClientId", "hostpanel");
        if (bearerIssuer is not null)
        {
            // Back-channel logout checks every logout token's issuer against it; startup refuses to proceed without it.
            builder.UseSetting("Auth:BearerIssuer", bearerIssuer);
        }

        builder.UseSetting("Auth:ClientSecret", "test-secret");
        builder.UseSetting("Auth:RedisConnection", redisConnection);
        builder.UseSetting("Sso:Authority", "https://sso.invalid");
        builder.UseSetting("Sso:ClientId", "hostpanel");
        builder.UseSetting("Sso:ServiceKey", "test-service-key");

        // Required whenever Auth:Mode is not local (BackchannelLogoutOptionsValidator), so an external sign-on provider host
        // refuses to boot without it.
        builder.UseSetting("BackchannelLogout:Key", "test-backchannel-key");

        // A host setting too: Wolverine reads the connection string for its message store while Program.cs
        // composes the builder, and a source added through ConfigureAppConfiguration only reaches the
        // configuration after Build(), so the host refused to start and none of these tests could run.
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
    }
}
