using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// <c>POST /api/admin/integrations/{slug}/test</c>: an integration with no required fields always
/// succeeds, an unconfigured one reports the missing fields while still answering 200, a failed
/// payment plugin probe never puts the gateway's own exception text on the wire, an unrecognised
/// slug is refused <c>INTEGRATION_NOT_FOUND</c>, a caller with only the Client role is refused, and the
/// route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TestIntegrationConnectionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An integration with no required fields is always reported as configured.</summary>
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownSlug_RefusesNotFoundAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/admin/integrations/unknown-{Guid.NewGuid():N}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await IntegrationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_NOT_FOUND);
    }

    /// <summary>
    /// A failed payment plugin probe still answers 200 and never puts the gateway's own exception text
    /// on the wire.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentPluginProbeFails_NeverLeaksGatewayDetailAsync()
    {
        var resolver = new FakePaymentPluginResolver();
        var plugin = new FakePaymentPlugin
        {
            StatusThrows = new InvalidOperationException("gateway detail nobody should see"),
        };
        resolver.Register("example-gateway", plugin);

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/admin/integrations/example-gateway/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("gateway detail nobody should see");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await IntegrationsTestSeeds.ClientRoleClientAsync(factory, "test-integration-forbidden");

        var response = await client.PostAsync("/api/admin/integrations/bank-transfer/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsync("/api/admin/integrations/bank-transfer/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
