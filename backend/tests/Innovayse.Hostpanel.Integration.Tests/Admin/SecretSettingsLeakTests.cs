using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// No endpoint that returns settings ever returns a secret value: the admin list and the admin read by
/// id (which do not return a secret row at all), the integration detail and the anonymous public settings. A credential is planted straight
/// in the database under a unique sentinel and every response body is searched for it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SecretSettingsLeakTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The setting key the gateway secret is planted under.</summary>
    private const string StripeSecretKey = "integration:stripe:secret_key";

    /// <summary>The admin list omits every row outside the catalogue, so a secret is not listed at all.</summary>
    [Fact]
    public async Task AdminList_OmitsSecretRowsAsync()
    {
        var sentinel = SecretSettingTestSeeds.Sentinel("list");
        await SecretSettingTestSeeds.StoreAsync(factory, StripeSecretKey, sentinel);
        await SecretSettingTestSeeds.StoreAsync(factory, SecretSettingKeys.SetupToken, sentinel);
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(sentinel);
        var rows = JsonSerializer.Deserialize<List<JsonElement>>(body)!;
        rows.Should().NotContain(r => r.GetProperty("key").GetString() == StripeSecretKey);
        rows.Should().NotContain(r => r.GetProperty("key").GetString() == SecretSettingKeys.SetupToken);
        rows.Should().OnlyContain(r => GeneralSettingKeys.Contains(r.GetProperty("key").GetString()!));
        rows.Single(r => r.GetProperty("key").GetString() == PortalSettingKeys.Template)
            .GetProperty("value").GetString().Should().NotBeNull();
    }

    /// <summary>The admin read by id answers a row outside the catalogue as not found, without its value.</summary>
    [Fact]
    public async Task AdminReadById_AnswersNotFoundForASecretRowAsync()
    {
        var sentinel = SecretSettingTestSeeds.Sentinel("by-id");
        var id = await SecretSettingTestSeeds.StoreAsync(factory, "integration:smtp:password", sentinel);
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/settings/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(sentinel);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SETTING_NOT_FOUND);
    }

    /// <summary>The integration detail answers a secret field with isSecret and hasValue, not a value or a mask.</summary>
    [Fact]
    public async Task IntegrationDetail_NeverReturnsASecretValueAsync()
    {
        var sentinel = SecretSettingTestSeeds.Sentinel("detail");
        await SecretSettingTestSeeds.StoreAsync(factory, StripeSecretKey, sentinel);
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/integrations/stripe");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(sentinel);
        using var document = JsonDocument.Parse(body);
        var config = document.RootElement.GetProperty("config");
        var secret = config.GetProperty("secret_key");
        secret.GetProperty("isSecret").GetBoolean().Should().BeTrue();
        secret.GetProperty("hasValue").GetBoolean().Should().BeTrue();
        secret.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
        config.TryGetProperty("webhook_secret", out _).Should().BeFalse();
        config.GetProperty("publishable_key").GetProperty("isSecret").GetBoolean().Should().BeFalse();
    }

    /// <summary>The anonymous public settings carry no secret and no integration row at all.</summary>
    [Fact]
    public async Task PublicSettings_NeverIncludeASecretAsync()
    {
        var sentinel = SecretSettingTestSeeds.Sentinel("public");
        await SecretSettingTestSeeds.StoreAsync(factory, StripeSecretKey, sentinel);
        await SecretSettingTestSeeds.StoreAsync(factory, SecretSettingKeys.SetupToken, sentinel);

        var response = await factory.CreateClient().GetAsync("/api/settings/public");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(sentinel);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows!.Should().OnlyContain(r => !SecretSettingKeys.IsSecret(r.GetProperty("key").GetString()!)
            && !r.GetProperty("key").GetString()!.StartsWith(IntegrationSettingKeys.RootPrefix, StringComparison.Ordinal));
    }
}
