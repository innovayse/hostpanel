using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Auth;

/// <summary>
/// Integration tests for the subject-keyed role store, against a real PostgreSQL
/// container.
///
/// <para>
/// Run here rather than as unit tests because what is worth checking is the mapping
/// itself: the table, the composite key, and the uniqueness that makes a repeated grant
/// harmless. An in-memory provider enforces none of those, so it would agree with a
/// mapping that Postgres rejects.
/// </para>
/// </summary>
public sealed class SubjectRoleStoreTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Runs <paramref name="body"/> against a role store resolved in a fresh scope.</summary>
    /// <typeparam name="T">What the body returns.</typeparam>
    /// <param name="factory">The shared test host.</param>
    /// <param name="body">The operation to run against the store.</param>
    /// <returns>Whatever the body returned.</returns>
    private static async Task<T> WithStoreAsync<T>(
        IntegrationTestFactory factory, Func<ISubjectRoleStore, Task<T>> body)
    {
        using var scope = factory.Services.CreateScope();
        return await body(scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>());
    }

    /// <summary>A granted role is visible to the next reader.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddAsync_ThenGetRoles_ReturnsTheRoleAsync()
    {
        var subject = $"sub-{Guid.NewGuid()}";

        var roles = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(subject, Role.Admin, CancellationToken.None);
            return await store.GetRolesAsync(subject, CancellationToken.None);
        });

        roles.Should().ContainSingle().Which.Should().Be(Role.Admin);
    }

    /// <summary>
    /// Granting the same role twice leaves one row and does not throw. The migration
    /// replays grants from the Identity role tables, and a re-run of it must be harmless.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddAsync_Twice_IsIdempotentAsync()
    {
        var subject = $"sub-{Guid.NewGuid()}";

        var roles = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(subject, Role.Reseller, CancellationToken.None);
            await store.AddAsync(subject, Role.Reseller, CancellationToken.None);
            return await store.GetRolesAsync(subject, CancellationToken.None);
        });

        roles.Should().ContainSingle().Which.Should().Be(Role.Reseller);
    }

    /// <summary>
    /// One subject's roles never appear against another. Trivial to state and the whole
    /// point of the table: every authorization decision downstream trusts this.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRolesAsync_DoesNotLeakBetweenSubjectsAsync()
    {
        var admin = $"sub-{Guid.NewGuid()}";
        var client = $"sub-{Guid.NewGuid()}";

        var (adminRoles, clientRoles) = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(admin, Role.Admin, CancellationToken.None);
            await store.AddAsync(client, Role.Client, CancellationToken.None);
            return (await store.GetRolesAsync(admin, CancellationToken.None),
                    await store.GetRolesAsync(client, CancellationToken.None));
        });

        adminRoles.Should().BeEquivalentTo([Role.Admin]);
        clientRoles.Should().BeEquivalentTo([Role.Client]);
    }

    /// <summary>A subject nobody has granted anything to holds no roles.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRolesAsync_ForAnUnknownSubject_ReturnsEmptyAsync()
    {
        var roles = await WithStoreAsync(factory, store =>
            store.GetRolesAsync($"sub-{Guid.NewGuid()}", CancellationToken.None));

        roles.Should().BeEmpty();
    }

    /// <summary>
    /// Revoking removes only what was named. A revoke that took the subject's other roles
    /// with it would lock someone out in a way no caller would think to check for.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveAsync_TakesOnlyTheNamedRoleAsync()
    {
        var subject = $"sub-{Guid.NewGuid()}";

        var roles = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(subject, Role.Admin, CancellationToken.None);
            await store.AddAsync(subject, Role.Reseller, CancellationToken.None);
            await store.RemoveAsync(subject, Role.Admin, CancellationToken.None);
            return await store.GetRolesAsync(subject, CancellationToken.None);
        });

        roles.Should().BeEquivalentTo([Role.Reseller]);
    }

    /// <summary>
    /// Backs the first-run bootstrap check. Runs inside one database transaction that is rolled
    /// back at the end: the existing <see cref="Role.Reseller"/> grants are removed only inside it,
    /// so the "nobody holds it" half is real, and no other test sharing the database ever sees the
    /// removal or the new grant.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnyHasRoleAsync_ReflectsWhetherAnyoneHoldsItAsync()
    {
        var subject = $"sub-{Guid.NewGuid()}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.SubjectRoles.Where(x => x.Role == Role.Reseller).ExecuteDeleteAsync();
        var before = await store.AnyHasRoleAsync(Role.Reseller, CancellationToken.None);

        await store.AddAsync(subject, Role.Reseller, CancellationToken.None);
        var after = await store.AnyHasRoleAsync(Role.Reseller, CancellationToken.None);

        await transaction.RollbackAsync();

        before.Should().BeFalse();
        after.Should().BeTrue();
    }
}
