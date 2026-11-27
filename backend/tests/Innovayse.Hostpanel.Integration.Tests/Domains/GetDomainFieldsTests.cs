using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>GetDomainFields</c> use case: <c>GET /api/domains/fields</c>, which answers the extra fields the
/// registrar plugin's manifest declares for a domain and an operation. Every zone in these tests is invented; the host reads
/// the zones from the manifest and holds none of its own.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDomainFieldsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A platform language other than the default one, taken from the supported list so no code is written here.</summary>
    private static readonly string OtherLanguage = SupportedLanguages.All.First(language => language != SupportedLanguages.Default);

    /// <summary>The locale texts of the manifest the tests share: a label key in the default language and in another one.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Locales =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [SupportedLanguages.Default] = new Dictionary<string, string>
            {
                ["holder.label"] = "Holder number",
                ["holder.hint"] = "As on the certificate",
            },
            [OtherLanguage] = new Dictionary<string, string> { ["holder.label"] = "Other holder number" },
        };

    /// <summary>Builds the one set every test declares: a select, a required text and a date for the zone, for registration only.</summary>
    /// <param name="zone">The invented zone.</param>
    /// <returns>The sets.</returns>
    private static IReadOnlyList<PluginDomainFieldSet> RegisterSet(string zone)
    {
        return
        [
            new PluginDomainFieldSet
            {
                Zones = [zone],
                Operations = PluginDomainOperations.Register,
                Fields =
                [
                    new PluginDomainField
                    {
                        Key = "holder-number",
                        Label = "holder.label",
                        Description = "holder.hint",
                        Type = PluginDomainFieldType.Text,
                        Required = true,
                        Pattern = "[A-Z]{2}[0-9]{4}",
                        MinLength = 6,
                        MaxLength = 6,
                    },
                    new PluginDomainField
                    {
                        Key = "category",
                        Label = "Category",
                        Type = PluginDomainFieldType.Select,
                        Options =
                        [
                            new PluginSettingOption { Value = "a", Label = "Category A" },
                            new PluginSettingOption { Value = "b", Label = "Category B" },
                        ],
                    },
                ],
            },
        ];
    }

    /// <summary>The fields come back in declared order with every rule the form needs, the texts resolved from the manifest locales.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetFields_ForADeclaredZoneAndOperation_AnswersTheFieldsWithTheirTextsAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, RegisterSet, Locales);

        var response = await scenario.Host.CreateClient()
            .GetAsync($"/api/domains/fields?domain={scenario.NewDomainName()}&operation=register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fields = body.RootElement.EnumerateArray().ToList();
        fields.Select(f => f.GetProperty("key").GetString()).Should().Equal("holder-number", "category");
        fields[0].GetProperty("label").GetString().Should().Be("Holder number");
        fields[0].GetProperty("description").GetString().Should().Be("As on the certificate");
        fields[0].GetProperty("type").GetString().Should().Be("Text");
        fields[0].GetProperty("required").GetBoolean().Should().BeTrue();
        fields[0].GetProperty("pattern").GetString().Should().Be("[A-Z]{2}[0-9]{4}");
        fields[0].GetProperty("minLength").GetInt32().Should().Be(6);
        fields[1].GetProperty("type").GetString().Should().Be("Select");
        fields[1].GetProperty("options").EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Equal("a", "b");
    }

    /// <summary>A text key is resolved in the request language, English where a language lacks it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetFields_ResolvesTextsInTheRequestLanguageAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, RegisterSet, Locales);
        var client = scenario.Host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Language", OtherLanguage);

        var response = await client.GetAsync($"/api/domains/fields?domain={scenario.NewDomainName()}&operation=register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = body.RootElement.EnumerateArray().First();
        first.GetProperty("label").GetString().Should().Be("Other holder number");
        first.GetProperty("description").GetString().Should().Be("As on the certificate");
    }

    /// <summary>An operation the set is not declared for answers no fields, so the form shows nothing extra.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetFields_ForAnOperationTheSetDoesNotCover_AnswersNoneAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, RegisterSet);

        var response = await scenario.Host.CreateClient()
            .GetAsync($"/api/domains/fields?domain={scenario.NewDomainName()}&operation=transfer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    /// <summary>A domain no registrar plugin asks anything for answers no fields, not a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetFields_ForAZoneNobodyConfigured_AnswersNoneAsync()
    {
        var response = await factory.CreateClient()
            .GetAsync($"/api/domains/fields?domain=name-{Guid.NewGuid():N}.{DomainsTestSeeds.UniqueTld()}&operation=register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    /// <summary>The answer is open to a caller who has not signed in, like the availability check the storefront runs first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetFields_WithoutASession_IsAnsweredAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, RegisterSet);

        var response = await scenario.Host.CreateClient()
            .GetAsync($"/api/domains/fields?domain={scenario.NewDomainName()}&operation=register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An operation that is none of register, transfer or contact change is refused by the validator.</summary>
    /// <param name="query">The query string after the domain.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("&operation=all")]
    [InlineData("&operation=register,transfer")]
    public async Task GetFields_AnOperationThatIsNotOne_Returns400Async(string query)
    {
        var response = await factory.CreateClient().GetAsync($"/api/domains/fields?domain=name-{Guid.NewGuid():N}.alpha{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>A text that is no domain name is refused before the manifest is read.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetFields_AMalformedName_Returns400Async()
    {
        var response = await factory.CreateClient().GetAsync("/api/domains/fields?domain=not%20a%20name&operation=register");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
