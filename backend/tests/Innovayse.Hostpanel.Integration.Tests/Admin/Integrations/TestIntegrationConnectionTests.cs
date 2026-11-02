using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// <c>POST /api/admin/integrations/{slug}/test</c>: an integration with no required fields always
/// succeeds, an unconfigured one reports the missing fields while still answering 200, a failed
/// live probe never puts the gateway's or a provider's own exception text on the wire, a built-in
/// integration with a probe is only probed once every required field is filled in, an unrecognised
/// slug is refused <c>INTEGRATION_NOT_FOUND</c>, a caller with only the Client role is refused, and the
/// route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TestIntegrationConnectionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An integration with no required fields is always reported as configured.</summary>
    [Fact]
    public async Task NoRequiredFields_AlwaysSucceedsAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync("/api/admin/integrations/bank-transfer/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    /// <summary>Testing an unconfigured integration reports the missing fields, still as 200.</summary>
    [Fact]
    public async Task AnUnconfiguredIntegration_ReportsMissingFieldsAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        const string slug = "maxmind";

        var response = await client.PostAsync($"/api/admin/integrations/{slug}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().Contain("account_id");
    }

    /// <summary>An unrecognised slug on the test route answers 404 with <c>INTEGRATION_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task UnknownSlug_RefusesNotFoundAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/admin/integrations/unknown-{Guid.NewGuid():N}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await IntegrationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_NOT_FOUND);
    }

    /// <summary>
    /// A failed hosted gateway probe still answers 200 and never puts the gateway's own exception text
    /// on the wire.
    /// </summary>
    [Fact]
    public async Task HostedGatewayProbeFails_NeverLeaksGatewayDetailAsync()
    {
        var resolver = new FakePaymentPluginResolver();
        var plugin = new FakePaymentPlugin
        {
            StatusThrows = new InvalidOperationException("gateway detail nobody should see"),
        };
        resolver.Register("inecobank", plugin);

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Planted directly: the hosted gateway is a plugin, and the save endpoint accepts only the
        // fields a loaded plugin declares, which this host need not have loaded.
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:inecobank:is_enabled", "true");
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:inecobank:gateway_url", "https://gateway.example.com");
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:inecobank:username", "merchant");
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:inecobank:password", "secret");

        var response = await client.PostAsync("/api/admin/integrations/inecobank/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("gateway detail nobody should see");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// A built-in integration with a live probe reports the credentials as accepted when the probe
    /// succeeds, and the probe was asked exactly once.
    /// </summary>
    [Fact]
    public async Task NameAmProbeSucceeds_ReportsCredentialsAcceptedAsync()
    {
        var probe = new FakeIntegrationConnectionProbe("nameam");
        var client = await ProbedAdminClientAsync(probe);
        await SaveNameAmConfigAsync(client, email: "user@example.com", password: "secret");

        var response = await client.PostAsync("/api/admin/integrations/nameam/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Contain("credentials accepted");
        probe.Calls.Should().Be(1);
    }

    /// <summary>
    /// A failed built-in probe still answers 200 with <c>success = false</c> and never puts the
    /// provider's own exception text on the wire.
    /// </summary>
    [Fact]
    public async Task NameAmProbeFails_NeverLeaksProviderDetailAsync()
    {
        var probe = new FakeIntegrationConnectionProbe("nameam")
        {
            Throws = new HttpRequestException("provider detail nobody should see: 401 (Unauthorized)"),
        };
        var client = await ProbedAdminClientAsync(probe);
        await SaveNameAmConfigAsync(client, email: "user@example.com", password: "wrong");

        var response = await client.PostAsync("/api/admin/integrations/nameam/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("provider detail nobody should see");

        // Assert on the user-facing field only: the raw body also carries a timestamp whose
        // fractional seconds can contain any digits, so a substring match on it is flaky.
        using var doc = JsonDocument.Parse(text);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString().Should().NotContain("401").And.NotContain("Unauthorized");
        probe.Calls.Should().Be(1);
    }

    /// <summary>
    /// A built-in integration with a required field left blank is reported as incomplete and the
    /// provider is never called.
    /// </summary>
    [Fact]
    public async Task NameAmMissingPassword_DoesNotProbeAsync()
    {
        var probe = new FakeIntegrationConnectionProbe("nameam");
        var client = await ProbedAdminClientAsync(probe);
        await SaveNameAmConfigAsync(client, email: "user@example.com", password: string.Empty);

        var response = await client.PostAsync("/api/admin/integrations/nameam/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().Contain("password");
        probe.Calls.Should().Be(0);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await IntegrationsTestSeeds.ClientRoleClientAsync(factory, "test-integration-forbidden");

        var response = await client.PostAsync("/api/admin/integrations/bank-transfer/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsync("/api/admin/integrations/bank-transfer/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Builds an admin client against a host where the given fake is the only connection probe, so the
    /// real provider's login is never reached over the network.
    /// </summary>
    /// <param name="probe">The fake probe to register in place of every real one.</param>
    /// <returns>A client carrying an admin bearer token.</returns>
    private async Task<HttpClient> ProbedAdminClientAsync(FakeIntegrationConnectionProbe probe)
    {
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIntegrationConnectionProbe>();
                services.AddSingleton<IIntegrationConnectionProbe>(probe);
            }));

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Saves the Name.am credentials through the admin Integrations route.</summary>
    /// <param name="client">An admin client.</param>
    /// <param name="email">The account e-mail to store.</param>
    /// <param name="password">
    /// The account password to store; blank clears it explicitly, because a blank secret on its own keeps what an
    /// earlier test in this class stored (the database is shared by the class).
    /// </param>
    /// <returns>A task that completes when the save was accepted.</returns>
    private static async Task SaveNameAmConfigAsync(HttpClient client, string email, string password)
    {
        var saved = await client.PutAsJsonAsync("/api/admin/integrations/nameam", new
        {
            isEnabled = true,
            config = new Dictionary<string, string>
            {
                ["email"] = email,
                ["password"] = password,
            },
            clearFields = password.Length == 0 ? new[] { "password" } : Array.Empty<string>(),
        });

        saved.IsSuccessStatusCode.Should().BeTrue();
    }
}
