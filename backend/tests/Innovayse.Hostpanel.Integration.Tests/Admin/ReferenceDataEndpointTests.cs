using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>ReferenceDataController</c>, moved from a bare <c>ControllerBase</c> with
/// <c>ApiController</c>/<c>Ok(</c> to <c>BaseApiController</c>'s <c>HandleData</c>, the sanctioned
/// way to answer 200 with a hardcoded list that never went through a <c>Result</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReferenceDataEndpointTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client carrying a seeded client's bearer token.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Countries are closed to an anonymous caller.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetCountries_RefusesAnonymousAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/reference/countries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An authenticated caller reads the full country list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetCountries_ReturnsTheListAsync()
    {
        var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync("/api/reference/countries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var countries = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        countries!.Should().Contain(c => c.GetProperty("code").GetString() == "US");
    }

    /// <summary>Currencies are readable by an anonymous caller: the storefront needs them before sign-in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetCurrencies_IsAnonymousAndReturnsTheListAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/reference/currencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var currencies = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        currencies!.Should().Contain(c => c.GetProperty("code").GetString() == "USD");
    }
}
