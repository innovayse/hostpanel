namespace Innovayse.Integration.Tests.Auth;

using FluentAssertions;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for the subject-keyed role store, against a real PostgreSQL
/// container.
///
/// <para>
/// Run here rather than as unit tests because what is worth checking is the mapping
/// itself — the table, the composite key, and the uniqueness that makes a repeated grant
/// harmless. An in-memory provider enforces none of those, so it would agree with a
/// mapping that Postgres rejects.
/// </para>
/// </summary>
public sealed class SubjectRoleStoreTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    private static async Task<T> WithStoreAsync<T>(
        IntegrationTestFactory factory, Func<ISubjectRoleStore, Task<T>> body)
    {
        using var scope = factory.Services.CreateScope();
        return await body(scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>());
    }

    /// <summary>A granted role is visible to the next reader.</summary>
    [Fact]
    public async Task AddAsync_ThenGetRoles_ReturnsTheRoleAsync()
    {
        var subject = $"sub-{Guid.NewGuid()}";

        var roles = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(subject, Roles.Admin, CancellationToken.None);
            return await store.GetRolesAsync(subject, CancellationToken.None);
        });

        roles.Should().ContainSingle().Which.Should().Be(Roles.Admin);
    }

    /// <summary>
    /// Granting the same role twice leaves one row and does not throw. The migration
    /// replays grants from the old Identity tables, and a re-run of it must be harmless.
    /// </summary>
    [Fact]
    public async Task AddAsync_Twice_IsIdempotentAsync()
    {
        var subject = $"sub-{Guid.NewGuid()}";

        var roles = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(subject, Roles.Reseller, CancellationToken.None);
            await store.AddAsync(subject, Roles.Reseller, CancellationToken.None);
            return await store.GetRolesAsync(subject, CancellationToken.None);
        });

        roles.Should().ContainSingle().Which.Should().Be(Roles.Reseller);
    }

    /// <summary>
    /// One subject's roles never appear against another. Trivial to state and the whole
    /// point of the table: every authorization decision downstream trusts this.
    /// </summary>
    [Fact]
    public async Task GetRolesAsync_DoesNotLeakBetweenSubjectsAsync()
    {
        var admin = $"sub-{Guid.NewGuid()}";
        var client = $"sub-{Guid.NewGuid()}";

        var (adminRoles, clientRoles) = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(admin, Roles.Admin, CancellationToken.None);
            await store.AddAsync(client, Roles.Client, CancellationToken.None);
            return (await store.GetRolesAsync(admin, CancellationToken.None),
                    await store.GetRolesAsync(client, CancellationToken.None));
        });

        adminRoles.Should().BeEquivalentTo([Roles.Admin]);
        clientRoles.Should().BeEquivalentTo([Roles.Client]);
    }

    /// <summary>A subject nobody has granted anything to holds no roles.</summary>
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
    [Fact]
    public async Task RemoveAsync_TakesOnlyTheNamedRoleAsync()
    {
        var subject = $"sub-{Guid.NewGuid()}";

        var roles = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(subject, Roles.Admin, CancellationToken.None);
            await store.AddAsync(subject, Roles.Reseller, CancellationToken.None);
            await store.RemoveAsync(subject, Roles.Admin, CancellationToken.None);
            return await store.GetRolesAsync(subject, CancellationToken.None);
        });

        roles.Should().BeEquivalentTo([Roles.Reseller]);
    }

    /// <summary>
    /// Backs the first-run bootstrap check. Uses a role name unique to this test rather
    /// than <see cref="Roles.Admin"/>, which other tests in this class also grant —
    /// reusing it here would make the "nobody holds it" half of this check depend on
    /// test ordering.
    /// </summary>
    [Fact]
    public async Task AnyHasRoleAsync_ReflectsWhetherAnyoneHoldsItAsync()
    {
        var role = $"role-{Guid.NewGuid()}";
        var subject = $"sub-{Guid.NewGuid()}";

        var before = await WithStoreAsync(factory, store => store.AnyHasRoleAsync(role, CancellationToken.None));

        var after = await WithStoreAsync(factory, async store =>
        {
            await store.AddAsync(subject, role, CancellationToken.None);
            return await store.AnyHasRoleAsync(role, CancellationToken.None);
        });

        before.Should().BeFalse();
        after.Should().BeTrue();
    }
}
