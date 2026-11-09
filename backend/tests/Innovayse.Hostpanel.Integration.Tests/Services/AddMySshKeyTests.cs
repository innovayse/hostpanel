using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><c>POST api/me/services/{id}/ssh/keys</c>: validation, routing to the right server and failure handling.</summary>
public sealed class AddMySshKeyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Posts a key to the add endpoint of a service.</summary>
    /// <param name="http">The signed-in client.</param>
    /// <param name="serviceId">The target service.</param>
    /// <param name="publicKey">The public key text.</param>
    /// <param name="label">The key label.</param>
    /// <returns>The raw response.</returns>
    private static Task<HttpResponseMessage> AddAsync(HttpClient http, int serviceId, string publicKey, string label = "laptop") =>
        http.PostAsJsonAsync($"/api/me/services/{serviceId}/ssh/keys", new { label, publicKey });

    /// <summary>A valid key answers 201 with a trimmed label, is installed without its comment only on the service's own server, and is written to the activity log.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ValidKey_IsInstalledOnlyOnTheServiceServerAndAuditedAsync()
    {
        var (http, clientId, serverA, serviceA) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-add-ok");
        var serverB = await SshTestSeeds.SeedServerAsync(factory, "b.example.test", 22);
        await SshTestSeeds.SeedServiceAsync(factory, clientId, serverB, "web");

        var response = await AddAsync(http, serviceA, SshTestSeeds.Key(1), "  work laptop ");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("label").GetString().Should().Be("work laptop");
        body.GetProperty("syncState").GetString().Should().Be("Installed");
        body.GetProperty("fingerprint").GetString().Should().StartWith("SHA256:");
        var installed = SshTestSeeds.Agents(factory).AgentFor(serverA).KeysOf("web").Values.Single();
        installed.Should().NotContain("me@laptop");
        SshTestSeeds.Agents(factory).AgentFor(serverB).Calls.Should().BeEmpty();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityLogs.AnyAsync(l => l.ClientId == clientId && l.Description.Contains("Added SSH key"))).Should().BeTrue();
    }

    /// <summary>An unsupported, malformed or private key answers 400 with its code and the server is never called.</summary>
    /// <param name="key">The rejected key text.</param>
    /// <param name="code">The expected error code.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQ me", "SSH_KEY_INVALID")]
    [InlineData("ssh-ed25519 not-base64", "SSH_KEY_INVALID")]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n-----END OPENSSH PRIVATE KEY-----", "SSH_PRIVATE_KEY_REJECTED")]
    public async Task BadKey_IsRefusedWithoutTouchingTheServerAsync(string key, string code)
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-add-bad");

        var response = await AddAsync(http, serviceId, key);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(code);
        SshTestSeeds.Agents(factory).AgentFor(serverId).Calls.Should().BeEmpty();
    }

    /// <summary>The same key blob added twice, even with another comment, answers 409 as already installed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SameKeyTwice_IsRefusedAsDuplicateAsync()
    {
        var (http, _, _, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-add-dup");
        (await AddAsync(http, serviceId, SshTestSeeds.Key(1))).EnsureSuccessStatusCode();

        var response = await AddAsync(http, serviceId, SshTestSeeds.Key(1).Replace("me@laptop", "other"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_KEY_ALREADY_INSTALLED);
    }

    /// <summary>Adding a key to another client's service answers 404 and the server is never called.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStrangersService_Returns404Async()
    {
        var (_, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-add-owner");
        var stranger = await DomainsTestSeeds.RegisterStrangerAsync(factory, "ssh-add-stranger");

        var response = await AddAsync(stranger, serviceId, SshTestSeeds.Key(1));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        SshTestSeeds.Agents(factory).AgentFor(serverId).Calls.Should().BeEmpty();
    }

    /// <summary>
    /// An agent refusal because the account may not use SSH (no shell, suspended on the server, or
    /// not eligible) answers 409 and leaves no key record.
    /// </summary>
    /// <param name="failure">The refusal the agent answers with.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(SshProviderFailure.SshDisabled)]
    [InlineData(SshProviderFailure.AccountSuspended)]
    [InlineData(SshProviderFailure.AccountNotEligible)]
    public async Task AgentAccessRefusal_LeavesNoRecordAsync(SshProviderFailure failure)
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, $"ssh-add-noshell-{failure}");
        SshTestSeeds.Agents(factory).AgentFor(serverId).FailWith = failure;

        var response = await AddAsync(http, serviceId, SshTestSeeds.Key(1));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        (await KeysOfAsync(serviceId)).Should().BeEmpty();
    }

    /// <summary>An unreachable server answers 503 and keeps an install-failed record with the failure on that server.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnreachableServer_KeepsAnInstallFailedRecordAsync()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-add-down");
        SshTestSeeds.Agents(factory).AgentFor(serverId).FailWith = SshProviderFailure.Unreachable;

        var response = await AddAsync(http, serviceId, SshTestSeeds.Key(1));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE);
        var record = (await KeysOfAsync(serviceId)).Single();
        record.SyncState.Should().Be(SshKeySyncState.InstallFailed);
        record.ServerId.Should().Be(serverId);
        record.LastSyncError.Should().Be(SshProviderFailure.Unreachable);
    }

    /// <summary>Extra body fields such as server, account or path are ignored, so the key lands only on the service account.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ExtraBodyFields_CannotRedirectTheKeyAsync()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-add-extra");

        var response = await http.PostAsJsonAsync($"/api/me/services/{serviceId}/ssh/keys", new
        {
            label = "x",
            publicKey = SshTestSeeds.Key(3),
            serverId = 999999,
            account = "root",
            path = "/root/.ssh",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        SshTestSeeds.Agents(factory).AgentFor(serverId).KeysOf("web").Should().HaveCount(1);
        SshTestSeeds.Agents(factory).AgentFor(serverId).KeysOf("root").Should().BeEmpty();
    }

    /// <summary>An empty label answers 400 with the validation error code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EmptyLabel_Returns400ValidationErrorAsync()
    {
        var (http, _, _, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-add-validation");

        var response = await AddAsync(http, serviceId, SshTestSeeds.Key(1), "");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Loads the stored key records of a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The key records.</returns>
    private async Task<List<SshKey>> KeysOfAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.SshKeys.AsNoTracking().Where(k => k.ClientServiceId == serviceId).ToListAsync();
    }
}
