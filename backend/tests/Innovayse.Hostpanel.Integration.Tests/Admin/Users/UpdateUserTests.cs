using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Users;

/// <summary>
/// <c>PUT /api/admin/users/{id}</c>: a profile update round-trips the new name and address, an
/// unknown subject answers 404, a taken address is refused before anything changes, the sessions
/// end only when the address moves, where
/// an external sign-on provider owns the accounts the write refuses with its own code, a caller with only the Client
/// role is refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class UpdateUserTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A profile update round-trips the new name and address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RoundTripsTheProfileAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, _) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());
        var newEmail = $"renamed-{Guid.NewGuid():N}@example.com";

        var update = await client.PutAsJsonAsync($"/api/admin/users/{subject}", new
        {
            firstName = "Renamed",
            lastName = "Person",
            email = newEmail,
            language = (string?)null,
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var read = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/users/{subject}");
        read.GetProperty("firstName").GetString().Should().Be("Renamed");
        read.GetProperty("email").GetString().Should().Be(newEmail);
    }

    /// <summary>An unknown subject is refused with <c>USER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownSubject_IsNotFoundAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}", new
        {
            firstName = "Someone",
            lastName = "Else",
            email = $"nobody-{Guid.NewGuid():N}@example.com",
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_NOT_FOUND);
    }

    /// <summary>An address another account holds is refused before anything changes, the name included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TakenEmail_IsRefusedAndChangesNothingAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, email) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());
        var (_, takenEmail) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.PutAsJsonAsync($"/api/admin/users/{subject}", new
        {
            firstName = "Changed",
            lastName = "Name",
            email = takenEmail,
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
        var read = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/users/{subject}");
        read.GetProperty("firstName").GetString().Should().Be("Jamie");
        read.GetProperty("email").GetString().Should().Be(email);
    }

    /// <summary>Saving the stored address again renames the person without ending their sessions.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SameEmail_KeepsTheSecurityStampAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, email) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());
        var before = await SecurityStampAsync(subject);

        var response = await client.PutAsJsonAsync($"/api/admin/users/{subject}", new
        {
            firstName = "Renamed",
            lastName = "Only",
            email,
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SecurityStampAsync(subject)).Should().Be(before);
    }

    /// <summary>Moving the address ends the person's sessions: the security stamp changes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NewEmail_RotatesTheSecurityStampAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, _) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());
        var before = await SecurityStampAsync(subject);

        var response = await client.PutAsJsonAsync($"/api/admin/users/{subject}", new
        {
            firstName = "Jamie",
            lastName = "Doe",
            email = $"moved-{Guid.NewGuid():N}@example.com",
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SecurityStampAsync(subject)).Should().NotBe(before);
    }

    /// <summary>An update with a malformed email is refused by the validator before it reaches the handler.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAMalformedEmailAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, _) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.PutAsJsonAsync($"/api/admin/users/{subject}", new
        {
            firstName = "Jamie",
            lastName = "Doe",
            email = "not-an-email",
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

#if INNOVAYSE_SSO
    /// <summary>Where an external sign-on provider owns the accounts, renaming one refuses with the change-name code.</summary>
    [Fact]
    public async Task UnderSso_RefusesWithItsOwnCodeAsync()
    {
        var host = UsersTestSeeds.SsoModeHost(factory, knownAccount: null);
        var client = UsersTestSeeds.SsoAdminClient(host);

        var response = await client.PutAsJsonAsync($"/api/admin/users/{Guid.NewGuid():N}", new
        {
            firstName = "Someone",
            lastName = "Else",
            email = "someone@example.com",
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_PROVISIONING_NOT_ALLOWED_CHANGE_NAME);
    }
#endif

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await UsersTestSeeds.ClientRoleClientAsync(factory, "update-user-forbidden");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}", new
        {
            firstName = "Someone",
            lastName = "Else",
            email = "someone@example.com",
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}", new
        {
            firstName = "Someone",
            lastName = "Else",
            email = "someone@example.com",
            language = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads the stored security stamp of a local account.</summary>
    /// <param name="subject">The account's subject.</param>
    /// <returns>The stamp.</returns>
    private async Task<string?> SecurityStampAsync(string subject)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return (await users.FindByIdAsync(subject))!.SecurityStamp;
    }
}
