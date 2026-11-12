using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary><c>PUT /api/admin/fraud/settings</c> against Postgres: the save, the range and ordering rules and the authorization boundary.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateFraudSettingsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>What a save writes is what the next read answers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SavedSettingsAreReadBack()
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, enabled: true, review: 30, reject: 70, windowMinutes: 15, maxOrders: 2, maxAccounts: 5);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var body = await admin.GetFromJsonAsync<JsonElement>("/api/admin/fraud/settings");

        body.GetProperty("enabled").GetBoolean().Should().BeTrue();
        body.GetProperty("manualReviewRiskScore").GetInt32().Should().Be(30);
        body.GetProperty("autoRejectRiskScore").GetInt32().Should().Be(70);
        body.GetProperty("velocityWindowMinutes").GetInt32().Should().Be(15);
        body.GetProperty("velocityMaxOrders").GetInt32().Should().Be(2);
        body.GetProperty("maxAccountsPerIp").GetInt32().Should().Be(5);
    }

    /// <summary>A review score that is not below the reject score is refused, and nothing is saved.</summary>
    /// <param name="review">The review score.</param>
    /// <param name="reject">The reject score.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(60, 60)]
    [InlineData(80, 60)]
    public async Task RefusesAReviewScoreNotBelowTheRejectScore(int review, int reject)
    {
        await FraudTestSeeds.SaveSettingsAsync(factory, enabled: false, review: 50, reject: 90);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await PutAsync(admin, review, reject);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var stored = await admin.GetFromJsonAsync<JsonElement>("/api/admin/fraud/settings");
        stored.GetProperty("manualReviewRiskScore").GetInt32().Should().Be(50);
        stored.GetProperty("autoRejectRiskScore").GetInt32().Should().Be(90);
    }

    /// <summary>A score outside 0 to 100 and a limit outside its range are refused.</summary>
    /// <param name="review">The review score.</param>
    /// <param name="reject">The reject score.</param>
    /// <param name="window">The velocity window in minutes.</param>
    /// <param name="maxOrders">The orders-per-window limit.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(-1, 90, 60, 3)]
    [InlineData(50, 101, 60, 3)]
    [InlineData(50, 90, 0, 3)]
    [InlineData(50, 90, 1441, 3)]
    [InlineData(50, 90, 60, 0)]
    [InlineData(50, 90, 60, 101)]
    public async Task RefusesValuesOutsideTheirRanges(int review, int reject, int window, int maxOrders)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await PutAsync(admin, review, reject, window, maxOrders);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Saving the settings is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await PutAsync(factory.CreateClient(), 50, 90);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var response = await PutAsync(await OrdersTestSeeds.ClientCallerAsync(factory), 50, 90);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Sends a settings body.</summary>
    /// <param name="client">The caller.</param>
    /// <param name="review">The review score.</param>
    /// <param name="reject">The reject score.</param>
    /// <param name="window">The velocity window in minutes.</param>
    /// <param name="maxOrders">The orders-per-window limit.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PutAsync(HttpClient client, int review, int reject, int window = 60, int maxOrders = 3)
    {
        return client.PutAsJsonAsync("/api/admin/fraud/settings", new
        {
            enabled = true,
            manualReviewRiskScore = review,
            autoRejectRiskScore = reject,
            velocityWindowMinutes = window,
            velocityMaxOrders = maxOrders,
            maxAccountsPerIp = 3,
        });
    }
}
