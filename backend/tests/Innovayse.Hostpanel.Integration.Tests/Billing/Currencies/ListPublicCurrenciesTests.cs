using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.Currencies;

/// <summary>
/// <c>GET /api/currencies</c> against Postgres: the anonymous storefront list, enabled rows only,
/// with no rate exposed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPublicCurrenciesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
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

    /// <summary>The storefront list is anonymous, lists only enabled rows, and carries no rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicListIsAnonymousEnabledOnlyAndRateless()
    {
        var admin = await AdminClientAsync();
        var disabled = await admin.PutAsJsonAsync("/api/admin/currencies/GBP",
            new { numeric = "826", prefix = "£", suffix = "", decimals = 2, rateToBase = 1.27m, isEnabled = false });
        disabled.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await factory.CreateClient().GetAsync("/api/currencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.EnumerateArray().ToList();
        var codes = rows.Select(r => r.GetProperty("code").GetString()).ToList();
        codes.Should().Contain(["USD", "AMD"]).And.NotContain("GBP");
        codes.First().Should().Be("USD");
        foreach (var row in rows)
        {
            row.EnumerateObject().Select(p => p.Name).Should()
                .NotContain("rateToBase")
                .And.Contain(["prefix", "suffix", "decimals", "isBase"]);
        }
    }
}
