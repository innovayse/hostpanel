using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/registry-instance/retry</c> against a fake registry: the first run generates the key pair, registers
/// its public half with the configured public address, serves the challenge at the well-known address and is confirmed; the private
/// key is stored encrypted and never sent; a run that fails is an answer with a code and a sentence; the registry asking for a fresh
/// start gets a new pair; a revoked installation registers again only on this explicit call.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RetryRegistryInstanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the retry.</summary>
    private const string Route = "/api/admin/plugins/registry-instance/retry";

    /// <summary>Reads the stored identity of a host straight through the store.</summary>
    /// <param name="host">The host.</param>
    /// <returns>The identity, or <see langword="null"/> when none is stored.</returns>
    private static async Task<RegistryInstanceRecord?> StoredAsync(RegistryTestHost host)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RegistryInstanceStore>().LoadAsync(CancellationToken.None);
    }

    /// <summary>The first run registers, serves the challenge, is verified, and reports a state that carries nothing secret.</summary>
    [Fact]
    public async Task TheFirstRun_RegistersAndIsVerifiedAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);

        var response = await admin.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        var instance = registry.Instances.Single();
        body.RootElement.GetProperty("state").GetString().Should().Be("Verified");
        body.RootElement.GetProperty("instanceId").GetGuid().Should().Be(instance.Id);
        body.RootElement.GetProperty("baseUrl").GetString().Should().Be(RegistryTestHost.PublicUrl);
        body.RootElement.GetProperty("errorCode").ValueKind.Should().Be(JsonValueKind.Null);
        instance.Verified.Should().BeTrue();
        instance.VerificationAttempts.Should().Be(1);
        var stored = await StoredAsync(host);
        text.Should().NotContain(stored!.PrivateKey);
        registry.Calls.Select(c => (c.Method, c.Target)).Should().Equal(
            ("POST", "/v1/instances"),
            ("POST", $"/v1/instances/{instance.Id:D}/verify"));
    }

    /// <summary>The registration sends the public key (32 bytes) and the configured address, and never the private key or a header of identity.</summary>
    [Fact]
    public async Task TheRegistration_SendsThePublicKeyAndTheAddressOnly_NeverThePrivateKeyAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);

        (await admin.PostAsync(Route, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await StoredAsync(host);
        var register = registry.Calls.Single(c => c.Target == "/v1/instances");
        using var sent = JsonDocument.Parse(register.Body);
        sent.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["publicKey", "baseUrl"]);
        sent.RootElement.GetProperty("publicKey").GetString().Should().Be(stored!.PublicKey);
        Convert.FromBase64String(stored.PublicKey).Should().HaveCount(32);
        sent.RootElement.GetProperty("baseUrl").GetString().Should().Be(RegistryTestHost.PublicUrl);
        registry.Calls.Should().OnlyContain(c => !c.Body.Contains(stored.PrivateKey) && !c.Target.Contains(stored.PrivateKey));
        registry.Calls.Should().OnlyContain(c => !c.IsSigned && !c.HasLicenceKey);
    }

    /// <summary>The identity is stored as one encrypted secret setting: not in plain text, readable only through the cipher.</summary>
    [Fact]
    public async Task TheIdentity_IsStoredEncrypted_ThePrivateKeyNotInPlainTextAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        (await admin.PostAsync(Route, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await StoredAsync(host);

        using var scope = host.Services.CreateScope();
        var raw = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Settings.AsNoTracking()
            .Where(s => s.Key == PluginSettingKeys.RegistryInstance)
            .Select(s => s.Value)
            .SingleAsync();

        raw.Should().StartWith("enc:v1:");
        raw.Should().NotContain(stored!.PrivateKey).And.NotContain(stored.PublicKey);
        SecretSettingKeys.IsSecret(PluginSettingKeys.RegistryInstance).Should().BeTrue();
        (await admin.GetStringAsync("/api/admin/settings")).Should().NotContain(PluginSettingKeys.RegistryInstance).And.NotContain(stored.PrivateKey);
    }

    /// <summary>A second run changes nothing: a verified identity is not registered again.</summary>
    [Fact]
    public async Task ASecondRun_DoesNotRegisterAgainAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        (await admin.PostAsync(Route, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var callsAfterFirst = registry.Calls.Count;

        var second = await admin.PostAsync(Route, null);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        registry.Calls.Should().HaveCount(callsAfterFirst);
        registry.Instances.Should().HaveCount(1);
    }

    /// <summary>A registry that cannot read the challenge leaves the state Error with the code, a sentence and the next attempt.</summary>
    [Fact]
    public async Task AChallengeTheRegistryCannotRead_IsAnErrorWithACodeAndASentenceAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        registry.FetchChallenge = _ => Task.FromResult<string?>(null);

        var response = await admin.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("state").GetString().Should().Be("Error");
        body.RootElement.GetProperty("errorCode").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE);
        body.RootElement.GetProperty("message").GetString().Should().Contain("/.well-known/hostpanel-registry/");
        body.RootElement.GetProperty("nextAttemptAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    /// <summary>When the challenge ran out of time the host registers again, with a new key pair, and is verified.</summary>
    [Fact]
    public async Task AnExpiredChallenge_RegistersAgainWithANewPairAsync()
    {
        var registry = new FakeRegistryServer { ExpireNextChallenge = true };
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);

        var response = await admin.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("state").GetString().Should().Be("Verified");
        registry.Instances.Should().HaveCount(2);
        registry.Instances[0].PublicKey.Should().NotEqual(registry.Instances[1].PublicKey);
        registry.Instances[1].Verified.Should().BeTrue();
        body.RootElement.GetProperty("instanceId").GetGuid().Should().Be(registry.Instances[1].Id);
    }

    /// <summary>A registry that already knows the key asks for a fresh start too: the pair is generated again.</summary>
    [Fact]
    public async Task AKeyTheRegistryAlreadyKnows_StartsOverWithANewPairAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        var refused = 0;
        registry.Intercept = call =>
        {
            if (call.Target == "/v1/instances" && refused++ == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("""{"type":"INSTANCE_KEY_ALREADY_REGISTERED"}""", System.Text.Encoding.UTF8, "application/json"),
                };
            }

            return null;
        };

        var response = await admin.PostAsync(Route, null);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("state").GetString().Should().Be("Verified");
        var keys = registry.Calls.Where(c => c.Target == "/v1/instances").Select(c => JsonDocument.Parse(c.Body).RootElement.GetProperty("publicKey").GetString()).ToList();
        keys.Should().HaveCount(2);
        keys[0].Should().NotBe(keys[1]);
    }

    /// <summary>While the registry is switched off the retry answers 409 and stores nothing.</summary>
    [Fact]
    public async Task WhileTheRegistryIsOff_TheRetryIsRefused_AndNothingIsStoredAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, enabled: false);
        var admin = await host.AdminClientAsync(factory);

        var response = await admin.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        registry.Calls.Should().BeEmpty();
        (await StoredAsync(host)).Should().BeNull();
    }

    /// <summary>An address the registry cannot use is told at once, with the sentence naming the setting, and no call is made.</summary>
    [Fact]
    public async Task AnAddressTheRegistryCannotUse_IsToldAtOnce_AndNoCallIsMadeAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, publicUrl: "http://localhost:3000");
        var admin = await host.AdminClientAsync(factory);

        var response = await admin.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("state").GetString().Should().Be("Error");
        body.RootElement.GetProperty("errorCode").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID);
        body.RootElement.GetProperty("message").GetString().Should().Contain("ClientBaseUrl");
        registry.Calls.Should().BeEmpty();
        (await StoredAsync(host)).Should().BeNull();
    }

    /// <summary>A revoked installation registers again on the explicit retry, with a new pair and a new instance.</summary>
    [Fact]
    public async Task ARevokedInstallation_RegistersAgainOnTheExplicitRetryAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        (await admin.PostAsync(Route, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var first = registry.Instances.Single();
        registry.Revoke(first.Id);
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRegistryRequestSigner>()
                .ReportRefusalAsync(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED, CancellationToken.None);
        }

        var response = await admin.PostAsync(Route, null);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("state").GetString().Should().Be(nameof(RegistryInstanceState.Verified));
        registry.Instances.Should().HaveCount(2);
        body.RootElement.GetProperty("instanceId").GetGuid().Should().Be(registry.Instances[1].Id);
        registry.Instances[1].PublicKey.Should().NotEqual(first.PublicKey);
    }

    /// <summary>A caller who is not an administrator is refused.</summary>
    [Fact]
    public async Task ACallerWhoIsNotAnAdministrator_IsRefusedAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "retry-instance");

        var response = await host.CreateClient().PostAsync(Route, null);
        var asClient = await client.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
        registry.Calls.Should().BeEmpty();
    }
}
