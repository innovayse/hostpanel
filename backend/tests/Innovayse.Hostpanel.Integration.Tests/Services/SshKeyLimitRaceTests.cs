using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// The per-service key limit holds under a race (<see cref="SshKeyInstaller"/>, shared by the client's add and the plugin
/// issue): an add that counted the last free slot while another add took it loses on the service row's token, counts
/// again and is refused with <c>SSH_KEY_LIMIT_REACHED</c>, and the service never holds more than the limit.
/// </summary>
/// <param name="fixture">The host with SSH-enabled servers.</param>
public sealed class SshKeyLimitRaceTests(PluginSshFixture fixture) : IClassFixture<PluginSshFixture>
{
    /// <summary>The last slot taken during the save: the loser answers 409, the agent never sees it, the limit holds.</summary>
    [Fact]
    public async Task LastSlotTakenDuringTheSave_IsRefusedAndTheLimitHoldsAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("ssh-limit-race");
        await SeedKeysAsync(owner, SshLimits.MaxKeysPerService - 1);
        var race = new SshKeyLimitRaceSaveInterceptor(owner.ServiceId);
        using var host = fixture.Host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));
        race.Attach(host.Services);
        using var http = AppPluginFixture.WithBearerOf(host, owner.Http);

        var response = await http.PostAsJsonAsync(
            $"/api/me/services/{owner.ServiceId}/ssh/keys", new { label = "loser", publicKey = SshTestSeeds.Key(251) });

        race.Fired.Should().BeTrue("the competing key must have been committed for the recount to run");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_KEY_LIMIT_REACHED);
        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.SshKeys.CountAsync(k => k.ClientServiceId == owner.ServiceId && k.SyncState != SshKeySyncState.Revoked))
            .Should().Be(SshLimits.MaxKeysPerService);
        (await db.SshKeys.AnyAsync(k => k.ClientServiceId == owner.ServiceId && k.Label == "loser")).Should().BeFalse();
    }

    /// <summary>
    /// A retry after a lost race forgets only the key and the service row it staged: something else the same request's
    /// context staged before the add (here a settings row, standing for a plugin call's store write) is still saved.
    /// </summary>
    [Fact]
    public async Task ARetry_KeepsWhatElseTheRequestStagedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("ssh-limit-race-keep");
        var race = new SshKeyLimitRaceSaveInterceptor(owner.ServiceId);
        using var host = fixture.Host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));
        race.Attach(host.Services);
        var settingKey = $"tests.ssh-race-keep.{Guid.NewGuid():N}";
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IHostpanelDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.ISshServiceContextResolver>();
        var installer = scope.ServiceProvider.GetRequiredService<SshKeyInstaller>();
        context.Settings.Add(Domain.Settings.Setting.Create(settingKey, "kept", null));
        Domain.Ssh.SshPublicKey.TryParse(SshTestSeeds.Key(252), out var publicKey, out _).Should().BeTrue();

        var installed = await installer.InstallAsync(
            (await resolver.ResolveAsync(owner.ServiceId, CancellationToken.None))!, "after race", publicKey!, null,
            CancellationToken.None);

        race.Fired.Should().BeTrue("the competing key must have been committed for the retry to run");
        installed.IsSuccess.Should().BeTrue();
        using var check = fixture.Host.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Settings.AnyAsync(row => row.Key == settingKey)).Should().BeTrue();
    }

    /// <summary>Stores installed keys on the service directly, standing for keys added earlier.</summary>
    /// <param name="owner">The service's owner scenario.</param>
    /// <param name="count">How many keys.</param>
    /// <returns>A task that completes once saved.</returns>
    private async Task SeedKeysAsync(SshOwnerScenario owner, int count)
    {
        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var i = 0; i < count; i++)
        {
            var key = SshKey.Create(
                owner.ServiceId, owner.ServerId, "web", $"seed {i}", SshTestSeeds.Key((byte)(i + 1)),
                $"SHA256:seed-{i}-{Guid.NewGuid():N}", issuedToPluginId: null);
            key.MarkInstalled();
            db.SshKeys.Add(key);
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }
}
