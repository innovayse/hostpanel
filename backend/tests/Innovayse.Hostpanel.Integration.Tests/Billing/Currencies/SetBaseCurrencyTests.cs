using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.Currencies;

/// <summary>
/// <c>POST /api/admin/currencies/{code}/make-base</c> against Postgres: promoting a currency to
/// base re-expresses every other rate, promoting the current base is a no-op, and an unconfigured
/// code cannot become the base.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetBaseCurrencyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>USD's rate once AMD is the base: 1 / 0.0025641, which is 390 to within a hundredth.</summary>
    private const decimal ExpectedUsdRateInAmd = 390m;

    /// <summary>How far a re-expressed rate may sit from its round figure at numeric(18,8).</summary>
    private const decimal RateTolerance = 0.01m;

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A code that names no configured currency cannot become the base.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MakeBaseRefusesAnUnconfiguredCurrency()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsync("/api/admin/currencies/ZZZ/make-base", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertCodeAsync(response, HostpanelErrorCodes.CURRENCY_NOT_FOUND);
    }

    /// <summary>Promoting the currency that is already the base changes nothing and is not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MakeBaseOnTheCurrentBaseIsANoOp()
    {
        var client = await AdminClientAsync();
        var before = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/currencies");
        var baseCode = before!.Single(r => r.GetProperty("isBase").GetBoolean()).GetProperty("code").GetString();

        var response = await client.PostAsync($"/api/admin/currencies/{baseCode}/make-base", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/currencies");
        after!.Single(r => r.GetProperty("isBase").GetBoolean())
            .GetProperty("code").GetString().Should().Be(baseCode);
    }

    /// <summary>
    /// Making AMD the base against Postgres: exactly one row is the base afterwards, it is AMD,
    /// and USD's rate is re-expressed as 1 / 0.0025641 ≈ 390. The base is put back to USD at the
    /// end because the fixture is shared and the other cases read the seeded pair.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MakeBaseSwitchesTheBaseAndReexpressesTheOtherRate()
    {
        var client = await AdminClientAsync();
        try
        {
            var response = await client.PostAsync("/api/admin/currencies/AMD/make-base", content: null);
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/currencies");
            list.Should().NotBeNull();
            var bases = list!.Where(r => r.GetProperty("isBase").GetBoolean()).ToList();
            bases.Should().ContainSingle();
            bases.Single().GetProperty("code").GetString().Should().Be("AMD");
            list!.Single(r => r.GetProperty("code").GetString() == "USD")
                .GetProperty("rateToBase").GetDecimal().Should().BeApproximately(ExpectedUsdRateInAmd, RateTolerance);
        }
        finally
        {
            var restored = await client.PostAsync("/api/admin/currencies/USD/make-base", content: null);
            restored.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    /// <summary>Promoting a currency to base is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MakeBaseRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/admin/currencies/AMD/make-base", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Asserts the refusal carries the expected error code, which is what a client branches on.
    /// </summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    private static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }
}
