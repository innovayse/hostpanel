using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <c>POST /api/auth/register</c> with fraud detection switched on, against Postgres: a registration that hits the block list is
/// refused and no account is created, one that only scores into the review band goes through, and both are logged.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RegisterFraudTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Registers from a fixed address.</summary>
    /// <param name="email">The new account's address.</param>
    /// <returns>The response.</returns>
    private Task<HttpResponseMessage> RegisterAsync(string email)
    {
        var host = FraudTestSeeds.WithCallerAt(factory, "198.51.100.80");
        return host.CreateClient().PostAsJsonAsync(
            "/api/auth/register", new { email, password = "Pass@123!", firstName = "Ada", lastName = "Lovelace" });
    }

    /// <summary>A registration from a blocked e-mail domain is refused with 403, creates no account and is logged as rejected.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ABlockListHit_RefusesTheRegistration()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync("/api/admin/fraud/blocklist", new { type = "EmailDomain", value = "no-signups.example" })).EnsureSuccessStatusCode();
        var email = $"new-{Guid.NewGuid():N}@no-signups.example";

        var response = await RegisterAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.REGISTRATION_FRAUD_REJECTED);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logged = await db.FraudLogEntries.AsNoTracking().SingleAsync(l => l.Email == email);
        logged.Decision.Should().Be(FraudDecision.Rejected);
        logged.Kind.Should().Be(FraudCheckKind.Registration);
        logged.IpAddress.Should().Be("198.51.100.80");
        (await db.Users.AsNoTracking().AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    /// <summary>A registration that only scores into the review band goes through, because there is no order to hold, and the log keeps it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AReviewScore_LetsTheRegistrationThroughAndLogsIt()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory);
        var email = $"new-{Guid.NewGuid():N}@mailinator.com";

        var response = await RegisterAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logged = await db.FraudLogEntries.AsNoTracking().SingleAsync(l => l.Email == email);
        logged.Decision.Should().Be(FraudDecision.Review);
        logged.Reasons.Should().Equal(FraudReasonCode.DisposableEmail);
    }

    /// <summary>With fraud detection off a registration is not checked or logged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenSwitchedOff_NothingIsLogged()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, enabled: false);
        var email = $"new-{Guid.NewGuid():N}@mailinator.com";

        var response = await RegisterAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.FraudLogEntries.AsNoTracking().AnyAsync(l => l.Email == email)).Should().BeFalse();
    }
}
