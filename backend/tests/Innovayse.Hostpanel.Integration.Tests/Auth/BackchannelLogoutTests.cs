using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <c>POST /auth/backchannel-logout</c>, the sign-on provider's sign-out fan-out. Only the
/// session store (an external port) is faked; the key check, validation, handler and identity
/// lookup are the real ones.
/// </summary>
/// <param name="factory">The shared host with a migrated database.</param>
public sealed class BackchannelLogoutTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The key the host under test is configured with.</summary>
    private const string Key = "test-backchannel-key";

    /// <summary>The route the portal posts to.</summary>
    private const string Route = "/auth/backchannel-logout";

    /// <summary>Builds a host configured with <see cref="Key"/> and recording revokers.</summary>
    /// <param name="revokers">Receive the revoked subjects; each is registered as one session store.</param>
    /// <returns>A client against that host.</returns>
    private HttpClient CreateClient(params RecordingSessionRevoker[] revokers)
    {
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackchannelLogout:Key", Key);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISessionRevoker>();
                foreach (var revoker in revokers)
                {
                    services.AddSingleton<ISessionRevoker>(revoker);
                }
            });
        });

        return host.CreateClient();
    }

    /// <summary>Registers a local account and returns its email and subject.</summary>
    /// <param name="client">A client against the host.</param>
    /// <returns>The address and the subject the identity provider names it by.</returns>
    private async Task<RegisteredSubject> RegisterAsync(HttpClient client)
    {
        var email = $"logout-{Guid.NewGuid():N}@example.com";
        var registered = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "Password123!", firstName = "Logout", lastName = "Person" });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var account = await identity.FindByEmailAsync(email, CancellationToken.None);
        return new(email, account!.Subject);
    }

    /// <summary>Sends the fan-out call, with the key header when one is given.</summary>
    /// <param name="client">A client against the host.</param>
    /// <param name="email">The address in the body.</param>
    /// <param name="key">The <c>X-Logout-Key</c> value, or null to omit the header.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string email, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(new { email }),
        };
        if (key is not null)
        {
            request.Headers.Add("X-Logout-Key", key);
        }

        return await client.SendAsync(request);
    }

    /// <summary>A wrong key is refused with 401 and revokes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WrongKeyRefusesAsync()
    {
        var revoker = new RecordingSessionRevoker();
        var client = CreateClient(revoker);
        var (email, _) = await RegisterAsync(client);

        var response = await PostAsync(client, email, "not-the-key");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        revoker.Revoked.Should().BeEmpty();
    }

    /// <summary>A missing key header is refused with 401 and revokes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingKeyRefusesAsync()
    {
        var revoker = new RecordingSessionRevoker();
        var client = CreateClient(revoker);
        var (email, _) = await RegisterAsync(client);

        var response = await PostAsync(client, email, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        revoker.Revoked.Should().BeEmpty();
    }

    /// <summary>The right key revokes the sessions of the person the address names.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ValidKeyRevokesTheSessionAsync()
    {
        var revoker = new RecordingSessionRevoker();
        var client = CreateClient(revoker);
        var (email, subject) = await RegisterAsync(client);

        var response = await PostAsync(client, email.ToUpperInvariant(), Key);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        revoker.Revoked.Should().Equal(subject);
    }

    /// <summary>
    /// Every registered session store is asked, so the client portal's sessions end together with
    /// the panel's own.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EverySessionStoreIsRevokedAsync()
    {
        var panel = new RecordingSessionRevoker();
        var portal = new RecordingSessionRevoker();
        var client = CreateClient(panel, portal);
        var (email, subject) = await RegisterAsync(client);

        var response = await PostAsync(client, email, Key);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        panel.Revoked.Should().Equal(subject);
        portal.Revoked.Should().Equal(subject);
    }

    /// <summary>An address no account answers is a success that revokes nothing, so a retry is harmless.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownUserIsIdempotentSuccessAsync()
    {
        var revoker = new RecordingSessionRevoker();
        var client = CreateClient(revoker);

        var response = await PostAsync(client, "nobody-here@example.com", Key);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        revoker.Revoked.Should().BeEmpty();
    }
}
