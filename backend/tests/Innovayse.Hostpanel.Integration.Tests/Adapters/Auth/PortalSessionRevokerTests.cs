using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Auth;

/// <summary>
/// <see cref="PortalSessionRevoker"/> against a real Redis container, seeded in the key layout the
/// client portal writes (<c>client/server/utils/session-store.ts</c>): a back-channel logout deletes
/// every session of the person and their id set, and nobody else's.
/// </summary>
public sealed class PortalSessionRevokerTests : IAsyncLifetime
{
    /// <summary>The Redis the portal and the revoker share.</summary>
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    /// <summary>A connection the test seeds and inspects Redis through.</summary>
    private ConnectionMultiplexer? _connection;

    /// <summary>Starts Redis and connects to it.</summary>
    /// <returns>A task that completes once Redis is reachable.</returns>
    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    /// <summary>Closes the connection and removes the container.</summary>
    /// <returns>A task that completes once the container is gone.</returns>
    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await _redis.DisposeAsync();
    }

    /// <summary>Gets the database the portal writes to.</summary>
    private IDatabase Database => _connection!.GetDatabase();

    /// <summary>Builds a revoker pointed at the container.</summary>
    /// <returns>The revoker.</returns>
    private PortalSessionRevoker CreateRevoker()
    {
        return new PortalSessionRevoker(Options.Create(new PortalSessionOptions { RedisConnection = _redis.GetConnectionString() }));
    }

    /// <summary>Stores a session the way the portal does: the entry, and its id in the owner's set.</summary>
    /// <param name="id">The opaque session id.</param>
    /// <param name="subject">The owner.</param>
    /// <returns>A task that completes once both keys are written.</returns>
    private async Task SeedSessionAsync(string id, string subject)
    {
        await Database.StringSetAsync($"hostpanel:portal-session:{id}", $"{{\"accessToken\":\"at-{id}\",\"subject\":\"{subject}\"}}", TimeSpan.FromHours(1));
        await Database.SetAddAsync($"hostpanel:portal-session-subject:{subject}", id);
    }

    /// <summary>Every session of the person and their id set are deleted; another person's stay.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RevokeAllAsync_DeletesEverySessionOfThePersonOnlyAsync()
    {
        await SeedSessionAsync("s1", "person-1");
        await SeedSessionAsync("s2", "person-1");
        await SeedSessionAsync("s3", "person-2");
        using var revoker = CreateRevoker();

        await revoker.RevokeAllAsync("person-1", CancellationToken.None);

        (await Database.KeyExistsAsync("hostpanel:portal-session:s1")).Should().BeFalse();
        (await Database.KeyExistsAsync("hostpanel:portal-session:s2")).Should().BeFalse();
        (await Database.KeyExistsAsync("hostpanel:portal-session-subject:person-1")).Should().BeFalse();
        (await Database.KeyExistsAsync("hostpanel:portal-session:s3")).Should().BeTrue();
        (await Database.SetMembersAsync("hostpanel:portal-session-subject:person-2")).Should().ContainSingle();
    }

    /// <summary>A person with no portal session is not an error, so a repeated fan-out is harmless.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RevokeAllAsync_WithNoSessionsSucceedsAsync()
    {
        using var revoker = CreateRevoker();

        var revoke = () => revoker.RevokeAllAsync("nobody", CancellationToken.None);

        await revoke.Should().NotThrowAsync();
    }

    /// <summary>The key prefixes are the ones the portal writes.</summary>
    [Fact]
    public void KeyPrefixes_MatchThePortalLayout()
    {
        PortalSessionRevoker.SessionKeyPrefix.Should().Be("hostpanel:portal-session:");
        PortalSessionRevoker.SubjectKeyPrefix.Should().Be("hostpanel:portal-session-subject:");
    }
}
