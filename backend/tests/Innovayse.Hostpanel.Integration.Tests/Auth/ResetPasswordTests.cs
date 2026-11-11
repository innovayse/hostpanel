using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <c>POST /api/auth/reset-password</c> (<c>ResetPasswordCommand</c>) on a deployment that is not a
/// demo: a valid token sets the password, anything else is <c>TOKEN_INVALID_OR_EXPIRED</c>. The demo
/// refusal is covered in <c>DemoAccountGuardTests</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ResetPasswordTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The password a successful reset sets.</summary>
    private const string NewPassword = "Reset-Pass-7!";

    /// <summary>Registers an account and returns its email with a valid reset token.</summary>
    /// <returns>The email and token.</returns>
    private async Task<PasswordResetAccount> RegisteredAccountAsync()
    {
        await BillingTestSeeds.RegisterClientAsync(factory, null, "reset-password");
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.Users.OrderByDescending(u => u.CreatedAt).FirstAsync();
        return new(user.Email!, await users.GeneratePasswordResetTokenAsync(user));
    }

    /// <summary>A valid token sets the new password and the account signs in with it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ValidTokenSetsThePasswordAsync()
    {
        var (email, token) = await RegisteredAccountAsync();
        var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email, password = NewPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A reset ends every session issued before it: a token from an earlier sign-in is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResetEndsTheSessionsIssuedBeforeAsync()
    {
        var http = factory.CreateClient();
        var email = $"reset-sessions-{Guid.NewGuid():N}@example.test";
        (await http.PostAsJsonAsync(
            "/api/auth/register", new { email, password = "Password123!", firstName = "Reset", lastName = "Sessions" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var earlier = await factory.GetClientTokenAsync(email, "Password123!");
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            token = await users.GeneratePasswordResetTokenAsync((await users.FindByEmailAsync(email))!);
        }

        (await http.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var signedIn = factory.CreateClient();
        signedIn.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", earlier);
        (await signedIn.GetAsync("/api/auth/email-verified")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A token that does not match is refused 400 <c>TOKEN_INVALID_OR_EXPIRED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidTokenIsRefusedAsync()
    {
        var (email, _) = await RegisteredAccountAsync();

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/reset-password", new { email, token = "not-a-token", newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
    }

    /// <summary>An email that names no account is refused the same way, so existence is not revealed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownEmailIsRefusedAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = "nobody-here@example.test", token = "whatever", newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
    }
}
