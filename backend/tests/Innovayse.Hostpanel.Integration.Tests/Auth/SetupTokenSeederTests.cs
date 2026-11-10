using Innovayse.Hostpanel.Application.Auth.Seeding;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <see cref="SetupTokenSeeder"/> against Postgres.
/// <para>
/// The property that matters most here is the one that is easiest to get wrong: the token must
/// <b>not</b> rotate on every boot. Rotating it would invalidate what an operator had already
/// copied, so restarting the container halfway through setup would lock them out of the step
/// they were in the middle of, which is exactly the failure mode this gate must not introduce.
/// </para>
/// </summary>
/// <remarks>
/// Runs the seeder against a real database through <see cref="ISetupTokenStore"/>. Each test builds its own
/// <see cref="IntegrationTestFactory"/>, the same as <c>CompleteSetupTests</c>: "nobody holds
/// Admin yet" is a real one-shot fact about an installation, not a row a test can reset.
/// </remarks>
public sealed class SetupTokenSeederTests
{
    /// <summary>Resolves the seeder's two real dependencies, and the context to assert on, from a scope.</summary>
    /// <param name="scope">The scope to resolve from.</param>
    /// <returns>The data context to assert on, the setup token store and the role store.</returns>
    private static SeederServices Resolve(IServiceScope scope) =>
        new(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            scope.ServiceProvider.GetRequiredService<ISetupTokenStore>(),
            scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
        );

    /// <summary>On a fresh install a token is issued, persisted and returned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureIssued_OnFreshInstall_IssuesAndPersistsAsync()
    {
        await using var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var (context, store, roles) = Resolve(scope);

        var token = await SetupTokenSeeder.EnsureIssuedAsync(store, roles);

        Assert.False(string.IsNullOrWhiteSpace(token));
        var added = await context.Settings.AsNoTracking()
            .SingleAsync(s => s.Key == SetupTokenSeeder.SettingKey);
        // Stored encrypted: the raw value is not the token, and decrypting it gives the token back.
        var cipher = scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>();
        Assert.NotEqual(token, added.Value);
        Assert.Equal(token, cipher.Reveal(added.Value));
    }

    /// <summary>
    /// A restart while setup is still outstanding re-announces the same token rather than
    /// issuing a new one. This is what makes a container restart mid-setup recoverable.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureIssued_WhenTokenOutstanding_ReturnsTheSameOneAsync()
    {
        await using var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var (_, store, roles) = Resolve(scope);
        var first = await SetupTokenSeeder.EnsureIssuedAsync(store, roles);

        var second = await SetupTokenSeeder.EnsureIssuedAsync(store, roles);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Once somebody holds Admin nothing is issued and nothing is announced: a completed
    /// installation must not keep printing a claimable secret to its log.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureIssued_WhenAdminHeld_IssuesNothingAsync()
    {
        await using var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var (context, store, roles) = Resolve(scope);
        await roles.AddAsync("someone-else", Role.Admin, CancellationToken.None);

        var token = await SetupTokenSeeder.EnsureIssuedAsync(store, roles);

        Assert.Null(token);
        Assert.False(await context.Settings.AsNoTracking().AnyAsync(s => s.Key == SetupTokenSeeder.SettingKey));
    }

    /// <summary>
    /// A spent row on an installation that is unclaimed again (setup completed, then every
    /// Admin grant revoked) is re-armed rather than left as a permanent lockout.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureIssued_WhenRowSpentAndAdminUnheld_ReArmsAsync()
    {
        await using var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var (context, store, roles) = Resolve(scope);
        context.Settings.Add(Setting.Create(SetupTokenSeeder.SettingKey, string.Empty, null));
        await context.SaveChangesAsync(CancellationToken.None);

        var token = await SetupTokenSeeder.EnsureIssuedAsync(store, roles);

        Assert.False(string.IsNullOrWhiteSpace(token));
        var spent = await context.Settings.AsNoTracking()
            .SingleAsync(s => s.Key == SetupTokenSeeder.SettingKey);
        Assert.Equal(token, scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>().Reveal(spent.Value));
    }

    /// <summary>
    /// Two issues never collide. The value is 256 bits of cryptographic randomness; this pins
    /// that it is generated rather than derived from anything stable.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureIssued_IssuesADistinctTokenPerInstallationAsync()
    {
        await using var factoryA = new IntegrationTestFactory();
        await factoryA.InitializeAsync();
        using var scopeA = factoryA.Services.CreateScope();
        var (_, storeA, rolesA) = Resolve(scopeA);

        await using var factoryB = new IntegrationTestFactory();
        await factoryB.InitializeAsync();
        using var scopeB = factoryB.Services.CreateScope();
        var (_, storeB, rolesB) = Resolve(scopeB);

        var first = await SetupTokenSeeder.EnsureIssuedAsync(storeA, rolesA);
        var second = await SetupTokenSeeder.EnsureIssuedAsync(storeB, rolesB);

        Assert.NotEqual(first, second);
    }
}
