using Innovayse.Hostpanel.Application.Auth.Seeding;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// Pins <see cref="ISetupTokenStore"/> against Postgres: the outstanding token is accepted once and
/// then retired, and anything else is refused without touching it.
/// </summary>
/// <remarks>
/// Each test builds its own <see cref="IntegrationTestFactory"/>, the same as
/// <c>SetupTokenSeederTests</c>: an outstanding setup token is a one-shot fact about an installation.
/// </remarks>
public sealed class SetupTokenStoreTests
{
    /// <summary>The issued token is accepted, then blanked, so a second use is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TryConsume_IssuedToken_AcceptsOnceAsync()
    {
        await using var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var token = await IssueAsync(scope);
        var store = scope.ServiceProvider.GetRequiredService<ISetupTokenStore>();

        (await store.TryConsumeAsync(token, CancellationToken.None)).Should().BeTrue();
        (await store.TryConsumeAsync(token, CancellationToken.None)).Should().BeFalse();

        var row = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Settings.AsNoTracking()
            .SingleAsync(s => s.Key == SetupTokenSeeder.SettingKey);
        row.Value.Should().BeEmpty();
    }

    /// <summary>A wrong or missing token is refused and leaves the outstanding one live.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TryConsume_WrongOrMissingToken_RefusesAndKeepsTheTokenAsync()
    {
        await using var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var token = await IssueAsync(scope);
        var store = scope.ServiceProvider.GetRequiredService<ISetupTokenStore>();

        (await store.TryConsumeAsync("not-the-token", CancellationToken.None)).Should().BeFalse();
        (await store.TryConsumeAsync(null, CancellationToken.None)).Should().BeFalse();

        (await store.TryConsumeAsync(token, CancellationToken.None)).Should().BeTrue();
    }

    /// <summary>Two consumes of the same token racing on separate connections: exactly one wins.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TryConsume_ConcurrentConsumes_ExactlyOneWinsAsync()
    {
        await using var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();
        string token;
        using (var issuing = factory.Services.CreateScope())
        {
            token = await IssueAsync(issuing);
        }

        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var outcomes = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<ISetupTokenStore>().TryConsumeAsync(token, CancellationToken.None),
            second.ServiceProvider.GetRequiredService<ISetupTokenStore>().TryConsumeAsync(token, CancellationToken.None));

        outcomes.Count(won => won).Should().Be(1);
    }

    /// <summary>Issues the installation's setup token through the real seeder.</summary>
    /// <param name="scope">The scope to resolve the context and role store from.</param>
    /// <returns>The issued token.</returns>
    private static async Task<string> IssueAsync(IServiceScope scope)
    {
        var token = await SetupTokenSeeder.EnsureIssuedAsync(
            scope.ServiceProvider.GetRequiredService<ISetupTokenStore>(),
            scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>());
        token.Should().NotBeNullOrWhiteSpace();
        return token!;
    }
}
