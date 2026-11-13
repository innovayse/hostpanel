using Innovayse.Hostpanel.Application.Auth.Queries.GetSetupRequired;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Persistence.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins where the development seed grants roles: in the store the setup check and authorization read, not
/// only in the Identity role table. Its own class, so it seeds a database of its own.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class DevDataSeederRolesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Sign-in address of the seeded administrator.</summary>
    private const string AdminEmail = "admin@example.test";

    /// <summary>
    /// The seeded admin holds the Admin role in <c>SubjectRoles</c>, so a fresh development database does
    /// not offer first-run setup.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SeedAsync_GrantsTheAdminRoleInTheSubjectRoleStore_SoSetupIsNotRequiredAsync()
    {
        using var scope = factory.Services.CreateScope();
        var passwords = Options.Create(new DevDataSeederOptions { AdminPassword = "Seed-admin-1", ClientPassword = "Seed-client-1" });
        var seeder = ActivatorUtilities.CreateInstance<DevDataSeeder>(scope.ServiceProvider, passwords);

        await seeder.SeedAsync();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = await users.FindByEmailAsync(AdminEmail);
        admin.Should().NotBeNull();
        var roles = scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>();
        (await roles.GetRolesAsync(admin!.Id, default)).Should().ContainSingle().Which.Should().Be(Role.Admin);

        var setup = await new GetSetupRequiredHandler(roles).HandleAsync(new GetSetupRequiredQuery(), default);
        setup.Value!.Required.Should().BeFalse();
    }
}
