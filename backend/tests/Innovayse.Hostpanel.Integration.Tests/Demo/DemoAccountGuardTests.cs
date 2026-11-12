using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo-account rule: while demo mode is on, the sign-in email and password of the two
/// configured demo accounts cannot change (403 <c>DEMO_ACTION_DISABLED</c>), through the password
/// reset and through a client edit; other profile fields stay editable. Wrong passwords never lock
/// those two accounts. With demo mode off the same calls behave exactly as before.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DemoAccountGuardTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The password a reset sets in the tests that expect it to land.</summary>
    private const string ChangedPassword = "Changed-Pass-9!";

    /// <summary>Derives a host that carries the demo options but has the switch off.</summary>
    /// <returns>A host whose guard must let everything through.</returns>
    private WebApplicationFactory<Program> DisabledHostWithDemoEmails() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Demo:Enabled", "false");
            builder.UseSetting("Demo:AdminEmail", DemoTestHost.AdminEmail);
            builder.UseSetting("Demo:ClientEmail", DemoTestHost.ClientEmail);
        });

    /// <summary>Seeds the published accounts on a demo host.</summary>
    /// <param name="host">The demo host.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private static async Task SeedDemoAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDemoDataManager>().SeedAsync(CancellationToken.None);
    }

    /// <summary>Makes sure a local account with the demo client's email exists, for the demo-off host.</summary>
    /// <param name="host">The host to create the account on.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private static async Task EnsureAccountAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(DemoTestHost.ClientEmail) is not null)
        {
            return;
        }

        var user = new AppUser
        {
            UserName = DemoTestHost.ClientEmail,
            Email = DemoTestHost.ClientEmail,
            FirstName = "Plain",
            LastName = "Customer",
            EmailConfirmed = true,
        };
        (await users.CreateAsync(user, DemoTestHost.ClientPassword)).Succeeded.Should().BeTrue();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Clients.Add(Client.Create(user.Id, "Plain", "Customer", DemoTestHost.ClientEmail, TestMoments.Seeded));
        await db.SaveChangesAsync();
    }

    /// <summary>Issues a valid password-reset token for the demo client's email.</summary>
    /// <param name="host">The host whose store holds the account.</param>
    /// <returns>The token.</returns>
    private static async Task<string> ResetTokenAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(DemoTestHost.ClientEmail);
        user.Should().NotBeNull();
        return await users.GeneratePasswordResetTokenAsync(user!);
    }

    /// <summary>Finds the id of the client row that belongs to the demo client's account.</summary>
    /// <param name="host">The host whose store holds the account.</param>
    /// <returns>The client id.</returns>
    private static async Task<int> DemoClientIdAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await users.FindByEmailAsync(DemoTestHost.ClientEmail);
        return await db.Clients.Where(c => c.UserId == user!.Id).Select(c => c.Id).FirstAsync();
    }

    /// <summary>Posts a password reset for the demo client's email.</summary>
    /// <param name="host">The host to call.</param>
    /// <param name="token">The reset token to send.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> ResetAsync(WebApplicationFactory<Program> host, string token) =>
        await host.CreateClient().PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = DemoTestHost.ClientEmail, token, newPassword = ChangedPassword });

    /// <summary>Demo on: resetting a demo account's password is refused and the old password still works.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_ResetPasswordOfADemoAccountIsRefusedAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await SeedDemoAsync(host);

        var response = await ResetAsync(host, await ResetTokenAsync(host));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.DemoActionDisabled);
        var login = await host.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = DemoTestHost.ClientEmail, password = DemoTestHost.ClientPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Demo on: resetting an ordinary account's password still works.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_ResetPasswordOfAnOrdinaryAccountStillWorksAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        var (_, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "demo-reset-ordinary");
        string email;
        string token;
        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.Users.OrderByDescending(u => u.CreatedAt).FirstAsync();
            email = user.Email!;
            token = await users.GeneratePasswordResetTokenAsync(user);
        }

        var response = await host.CreateClient().PostAsJsonAsync(
            "/api/auth/reset-password", new { email, token, newPassword = ChangedPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Demo on: a client edit that changes the demo client's email is refused; the email is untouched.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_ChangingTheDemoClientEmailIsRefusedAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await SeedDemoAsync(host);
        var clientId = await DemoClientIdAsync(host);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(email: "someone-else@demo.example"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.DemoActionDisabled);
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        (await users.FindByEmailAsync(DemoTestHost.ClientEmail)).Should().NotBeNull();
    }

    /// <summary>Demo on: the same edit with the email unchanged still saves the other profile fields.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_OtherProfileFieldsStayEditableAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await SeedDemoAsync(host);
        var clientId = await DemoClientIdAsync(host);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(firstName: "Renamed", email: DemoTestHost.ClientEmail));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Demo off: the reset sets the password for an account whose email matches the unused demo options.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenDisabled_ResetPasswordWorksAsync()
    {
        var host = DisabledHostWithDemoEmails();
        await EnsureAccountAsync(host);

        var response = await ResetAsync(host, await ResetTokenAsync(host));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await host.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = DemoTestHost.ClientEmail, password = ChangedPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Demo off: a bad token is still the ordinary 400 <c>TOKEN_INVALID_OR_EXPIRED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenDisabled_ResetPasswordWithABadTokenIsRefusedAsBeforeAsync()
    {
        var host = DisabledHostWithDemoEmails();
        await EnsureAccountAsync(host);

        var response = await ResetAsync(host, "not-a-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
    }

    /// <summary>Demo off: the client email change is allowed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenDisabled_ChangingTheClientEmailIsAllowedAsync()
    {
        var host = DisabledHostWithDemoEmails();
        await EnsureAccountAsync(host);
        var clientId = await DemoClientIdAsync(host);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(email: $"moved-{Guid.NewGuid():N}@example.test"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Demo on: wrong passwords past the lockout cap never lock a published demo account.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WhenEnabled_WrongPasswordsNeverLockADemoAccountAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await SeedDemoAsync(host);
        var client = host.CreateClient();

        for (var attempt = 0; attempt <= new SignInLockoutOptions().MaxFailedAttempts; attempt++)
        {
            var wrong = await client.PostAsJsonAsync(
                "/api/auth/login", new { email = DemoTestHost.ClientEmail, password = "Wrong-password-1" });
            wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = DemoTestHost.ClientEmail, password = DemoTestHost.ClientPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
