using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginServiceConnectionReader"/> against Postgres: the container resolves it, a service is read with its
/// server and the decrypted credentials, and a service with no server or a deleted one still answers, without a server.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginServiceConnectionReaderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The password the seeded server stores.</summary>
    private const string StoredPassword = "reader-password-2c9e";

    /// <summary>The production container resolves the reader for the port.</summary>
    [Fact]
    public void Container_ResolvesThePluginServiceConnectionReader()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPluginServiceConnectionReader>().Should().BeOfType<PluginServiceConnectionReader>();
    }

    /// <summary>A service with a server is read with its owner, product, status, reference and the server's decrypted credentials.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceWithAServer_IsReadWithTheStoredCredentialsAsync()
    {
        var owner = await SshTestSeeds.OwnerWithServiceAsync(factory, "reader-ok", new ServerCredentials(StoredPassword, "reader-token", "reader-hash"));

        var read = await ReadAsync(owner.ServiceId);

        read.Should().NotBeNull();
        read!.ClientId.Should().Be(owner.ClientId);
        read.Status.Should().Be(ServiceStatus.Active);
        read.ProvisioningRef.Should().Be("web");
        read.Server.Should().NotBeNull();
        read.Server!.Connection.Id.Should().Be(owner.ServerId);
        read.Server.Connection.ApiToken.Should().Be("reader-token");
        read.Server.Connection.AccessHash.Should().Be("reader-hash");
        read.Server.Password.Should().Be(StoredPassword);
        read.Server.Connection.UseSsl.Should().BeTrue();
    }

    /// <summary>A service whose server row is gone answers with the service and no server.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceWithADeletedServer_IsReadWithoutAServerAsync()
    {
        var owner = await SshTestSeeds.OwnerWithServiceAsync(factory, "reader-gone");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.Servers.Where(s => s.Id == owner.ServerId).ExecuteDeleteAsync();
        }

        var read = await ReadAsync(owner.ServiceId);

        read.Should().NotBeNull();
        read!.ServiceId.Should().Be(owner.ServiceId);
        read.Server.Should().BeNull();
    }

    /// <summary>An id with no service reads as null.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingService_ReadsAsNullAsync()
    {
        (await ReadAsync(int.MaxValue)).Should().BeNull();
    }

    /// <summary>Reads a service through the port the host resolves.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>What the reader answered.</returns>
    private async Task<PluginServiceConnection?> ReadAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPluginServiceConnectionReader>()
            .ResolveAsync(serviceId, CancellationToken.None);
    }
}
