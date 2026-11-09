using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Persistence.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins the development seed's handling of accounts: a sign-in account that already exists still gets
/// its client record, and keeps the password it has.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class DevDataSeederTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Sign-in address of the first seeded client.</summary>
    private const string FirstClientEmail = "owner@example.test";

    /// <summary>Password the pre-existing account was created with.</summary>
    private const string ExistingPassword = "Existing-pass-1";

    /// <summary>
    /// The account behind a seeded client already exists, as after a run that stopped half way: the seed
    /// creates the client record for it, does not reset its password, and the client is not skipped.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SeedAsync_WhenTheAccountAlreadyExists_StillCreatesItsClientAndKeepsThePasswordAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var existing = new AppUser
        {
            UserName = FirstClientEmail,
            Email = FirstClientEmail,
            FirstName = "Owner",
            LastName = "One",
            EmailConfirmed = true,
        };
        (await users.CreateAsync(existing, ExistingPassword)).Succeeded.Should().BeTrue();

        var passwords = Options.Create(new DevDataSeederOptions { AdminPassword = "Seed-admin-1", ClientPassword = "Seed-client-1" });
        var seeder = ActivatorUtilities.CreateInstance<DevDataSeeder>(scope.ServiceProvider, passwords);
        await seeder.SeedAsync();

        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Clients.AsNoTracking().AnyAsync(c => c.UserId == existing.Id)).Should().BeTrue();
        (await users.CheckPasswordAsync(existing, ExistingPassword)).Should().BeTrue();
    }
}
