using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.Currencies;

/// <summary>
/// <c>GET /api/admin/currencies</c> against Postgres: the migration-seeded USD/AMD pair, with rates.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListCurrenciesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>The migration seeded USD as the base and AMD beside it; the admin list shows both, with rates.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListShowsTheSeededPairWithRates()
    {
        var client = await AdminClientAsync();

        var response = await client.GetAsync("/api/admin/currencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("code").GetString()).Should().Contain(["USD", "AMD"]);
        rows.Single(r => r.GetProperty("code").GetString() == "USD").GetProperty("isBase").GetBoolean().Should().BeTrue();
        rows.Single(r => r.GetProperty("code").GetString() == "AMD").GetProperty("rateToBase").GetDecimal().Should().Be(0.00256410m);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/currencies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
