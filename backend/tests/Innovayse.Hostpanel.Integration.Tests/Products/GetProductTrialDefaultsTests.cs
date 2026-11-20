using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/products/trial-defaults</c>: the defaults and bounds the admin product form is built from are the ones the
/// domain enforces, and the route is for staff only.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductTrialDefaultsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The defaults and bounds are answered to an admin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTrialDefaults_AnswersTheDomainDefaultsAndBounds()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var defaults = await client.GetFromJsonAsync<JsonElement>("/api/products/trial-defaults");

        defaults.GetProperty("days").GetInt32().Should().Be(14);
        defaults.GetProperty("autoConvert").GetBoolean().Should().BeTrue();
        defaults.GetProperty("reminderDays").EnumerateArray().Select(d => d.GetInt32()).Should().Equal(3, 1);
        defaults.GetProperty("gracePeriodDays").GetInt32().Should().Be(3);
        defaults.GetProperty("minDays").GetInt32().Should().Be(1);
        defaults.GetProperty("maxDays").GetInt32().Should().Be(365);
        defaults.GetProperty("minGracePeriodDays").GetInt32().Should().Be(0);
        defaults.GetProperty("maxGracePeriodDays").GetInt32().Should().Be(30);
        defaults.GetProperty("maxReminders").GetInt32().Should().Be(5);
    }

    /// <summary>Without a session the defaults are not answered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTrialDefaults_WithoutASession_IsRefused()
    {
        var response = await factory.CreateClient().GetAsync("/api/products/trial-defaults");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
