using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary><c>GET /api/admin/fraud/settings</c> against Postgres: the defaults of a fresh install and the authorization boundary.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetFraudSettingsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Settings nobody saved read as their defaults: off, review at 50, reject at 90.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ANeverSavedInstallReadsTheDefaults()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/admin/fraud/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        body.GetProperty("manualReviewRiskScore").GetInt32().Should().Be(50);
        body.GetProperty("autoRejectRiskScore").GetInt32().Should().Be(90);
        body.GetProperty("velocityWindowMinutes").GetInt32().Should().Be(60);
        body.GetProperty("velocityMaxOrders").GetInt32().Should().Be(3);
        body.GetProperty("maxAccountsPerIp").GetInt32().Should().Be(3);
    }

    /// <summary>Reading the settings is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/fraud/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync("/api/admin/fraud/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
