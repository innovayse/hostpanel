using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// <c>PUT /api/admin/integrations/{slug}</c>: an admin upserts an integration's configuration,
/// re-saving a secret field empty keeps the stored secret, a new value replaces it, an explicit
/// clear removes it, a secret is stored encrypted, an unknown slug answers 404, an undeclared or
/// case-variant field answers INTEGRATION_FIELD_UNKNOWN and a value that does not fit its type
/// INTEGRATION_FIELD_INVALID (both with nothing written), a caller with only the Client role is refused, and the route is closed to a
/// caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SaveIntegrationConfigTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Re-saving with the secret field empty leaves the previously stored secret untouched.</summary>
    [Fact]
    public async Task SaveWithEmptySecret_KeepsTheStoredSecretAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        var slug = "namecheap";

        await client.PutAsJsonAsync($"/api/admin/integrations/{slug}", new
        {
            isEnabled = false,
            config = new Dictionary<string, string>
            {
                ["api_key"] = "original-secret-value",
                ["api_username"] = "someone",
                ["client_ip"] = "1.2.3.4",
            },
        });

        var resave = await client.PutAsJsonAsync($"/api/admin/integrations/{slug}", new
        {
            isEnabled = true,
            config = new Dictionary<string, string>
            {
                ["api_key"] = string.Empty,
                ["api_username"] = "someone-else",
                ["client_ip"] = "1.2.3.4",
            },
        });
        resave.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var test = await client.PostAsync($"/api/admin/integrations/{slug}/test", null);
        var body = await test.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue("the original key is still stored");
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:namecheap:api_key")).Should().Be("original-secret-value");
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:namecheap:api_username")).Should().Be("someone-else");
    }

    /// <summary>A secret field left out of the body keeps the stored value too.</summary>
    [Fact]
    public async Task SaveWithoutTheSecretField_KeepsTheStoredSecretAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:maxmind:license_key", "kept-licence");

        var save = await client.PutAsJsonAsync("/api/admin/integrations/maxmind", new
        {
            isEnabled = false,
            config = new Dictionary<string, string> { ["account_id"] = "42" },
        });

        save.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:maxmind:license_key")).Should().Be("kept-licence");
    }

    /// <summary>A new value for a secret field replaces the stored one.</summary>
    [Fact]
    public async Task SaveWithNewSecret_ReplacesTheStoredSecretAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:enom:api_key", "old-gateway-key");

        var save = await client.PutAsJsonAsync("/api/admin/integrations/smtp", new
        {
            isEnabled = false,
            config = new Dictionary<string, string> { ["client_secret"] = "new-gateway-key" },
        });

        save.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:enom:api_key")).Should().Be("new-gateway-key");
    }

    /// <summary>An explicit clear removes the stored secret, and the detail then reports it as not set.</summary>
    [Fact]
    public async Task SaveWithClear_RemovesTheStoredSecretAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:maxmind:license_key", "licence-to-clear");

        var save = await client.PutAsJsonAsync("/api/admin/integrations/maxmind", new
        {
            isEnabled = false,
            config = new Dictionary<string, string> { ["license_key"] = "ignored-because-cleared" },
            clearFields = new[] { "license_key" },
        });

        save.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:maxmind:license_key")).Should().BeEmpty();
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/admin/integrations/maxmind");
        detail.GetProperty("config").GetProperty("license_key").GetProperty("hasValue").GetBoolean().Should().BeFalse();
    }

    /// <summary>A saved secret is stored encrypted: the raw column differs from the input, the read returns it.</summary>
    [Fact]
    public async Task SaveSecret_StoresItEncryptedAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        var secret = SecretSettingTestSeeds.Sentinel("cpanel-token");

        var save = await client.PutAsJsonAsync("/api/admin/integrations/cpanel", new
        {
            isEnabled = false,
            config = new Dictionary<string, string> { ["api_token"] = secret },
        });

        save.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var raw = await SecretSettingTestSeeds.ReadRawAsync(factory, "integration:cpanel:api_token");
        raw.Should().NotBeNullOrEmpty();
        raw.Should().NotBe(secret).And.NotContain(secret);
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:cpanel:api_token")).Should().Be(secret);
    }

    /// <summary>A slug that names neither a built-in integration nor a loaded plugin is refused with 404.</summary>
    [Fact]
    public async Task UnknownSlug_IsNotFoundAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        var slug = $"unknown-{Guid.NewGuid():N}";

        var response = await client.PutAsJsonAsync($"/api/admin/integrations/{slug}", new
        {
            isEnabled = true,
            config = new Dictionary<string, string> { ["anything"] = "value" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SecretSettingTestSeeds.ReadAsync(factory, $"integration:{slug}:anything")).Should().BeNull();
    }

    /// <summary>Sends an enabling save for an integration and returns the response.</summary>
    /// <param name="client">The admin client.</param>
    /// <param name="slug">The integration slug.</param>
    /// <param name="config">The field values.</param>
    /// <param name="clearFields">The fields to clear.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> SaveAsync(
        HttpClient client, string slug, Dictionary<string, string> config, string[]? clearFields = null)
    {
        return client.PutAsJsonAsync($"/api/admin/integrations/{slug}", new { isEnabled = true, config, clearFields });
    }

    /// <summary>Asserts nothing was stored for an integration after a refused save, not even the switch.</summary>
    /// <param name="slug">The integration slug.</param>
    /// <param name="fields">The field keys that must have no row.</param>
    /// <returns>A task that completes when the check is done.</returns>
    private async Task AssertNothingStoredAsync(string slug, params string[] fields)
    {
        foreach (var field in fields.Append("is_enabled"))
        {
            (await SecretSettingTestSeeds.ReadAsync(factory, $"integration:{slug}:{field}")).Should().BeNull($"{field} must not be written");
        }
    }

    /// <summary>Reads the stored value of each field so a later read can prove a refused save changed none of them.</summary>
    /// <param name="slug">The integration slug.</param>
    /// <param name="fields">The field keys to read, plus the enabled switch.</param>
    /// <returns>The stored values in field order, <c>null</c> for a field with no row.</returns>
    private async Task<List<string?>> SnapshotAsync(string slug, params string[] fields)
    {
        var values = new List<string?>();
        foreach (var field in fields.Append("is_enabled"))
        {
            values.Add(await SecretSettingTestSeeds.ReadAsync(factory, $"integration:{slug}:{field}"));
        }

        return values;
    }

    /// <summary>A field the integration does not declare is refused INTEGRATION_FIELD_UNKNOWN and nothing is written.</summary>
    [Fact]
    public async Task UnknownKey_IsRefusedAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "cpanel", new() { ["not_a_field"] = "value" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_UNKNOWN);
        await AssertNothingStoredAsync("cpanel", "not_a_field");
    }

    /// <summary>The reserved switch key cannot be written as a field.</summary>
    [Fact]
    public async Task IsEnabledKey_IsRefusedAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "bank-transfer", new() { ["is_enabled"] = "true" });

        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_UNKNOWN);
        await AssertNothingStoredAsync("bank-transfer");
    }

    /// <summary>A case variant of a declared key is a different key and is refused.</summary>
    [Fact]
    public async Task CaseVariantKey_IsRefusedAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "enom", new() { ["Account_ID"] = "x" });

        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_UNKNOWN);
        await AssertNothingStoredAsync("enom", "Account_ID", "account_id");
    }

    /// <summary>An undeclared field in ClearFields is refused the same way.</summary>
    [Fact]
    public async Task UndeclaredClearField_IsRefusedAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "resellerclub", new() { ["reseller_id"] = "42" }, ["Reseller_ID"]);

        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_UNKNOWN);
        await AssertNothingStoredAsync("resellerclub", "reseller_id");
    }

    /// <summary>A valid field next to an undeclared one writes nothing at all.</summary>
    [Fact]
    public async Task MixedValidAndUnknown_WritesNothingAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "resellerclub", new() { ["reseller_id"] = "7", ["bogus"] = "x" });

        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_UNKNOWN);
        await AssertNothingStoredAsync("resellerclub", "reseller_id", "bogus");
    }

    /// <summary>A valid field next to an invalid value writes nothing at all.</summary>
    [Fact]
    public async Task MixedValidAndInvalid_WritesNothingAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var before = await SnapshotAsync("smtp", "host", "encryption");

        var response = await SaveAsync(client, "smtp", new() { ["host"] = "mail.example.test", ["encryption"] = "Staging" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_INVALID);
        (await SnapshotAsync("smtp", "host", "encryption")).Should().Equal(before, "a refused save writes nothing");
    }

    /// <summary>A toggle value that is not true or false is refused INTEGRATION_FIELD_INVALID, naming the field.</summary>
    [Fact]
    public async Task InvalidToggle_IsRefusedWithTheExpectationAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "nameam", new() { ["test_mode"] = "maybe" });

        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_INVALID);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors").GetProperty("test_mode").ToString().Should().Contain("Expected: ");
    }

    /// <summary>A select value that is not an option is refused INTEGRATION_FIELD_INVALID.</summary>
    [Fact]
    public async Task InvalidSelect_IsRefusedAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var before = await SnapshotAsync("smtp", "encryption");

        var response = await SaveAsync(client, "smtp", new() { ["encryption"] = "Staging" });

        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_FIELD_INVALID);
        (await SnapshotAsync("smtp", "encryption")).Should().Equal(before, "a refused save writes nothing");
    }

    /// <summary>A select value in another case is stored in the declared spelling.</summary>
    [Fact]
    public async Task SelectCaseVariant_IsStoredInTheDeclaredSpellingAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "smtp", new() { ["encryption"] = "ssl" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:smtp:encryption")).Should().Be("SSL");
    }

    /// <summary>A toggle is stored lowercased.</summary>
    [Fact]
    public async Task Toggle_IsStoredLowercaseAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await SaveAsync(client, "nameam", new() { ["test_mode"] = "TRUE" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SecretSettingTestSeeds.ReadAsync(factory, "integration:nameam:test_mode")).Should().Be("true");
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await IntegrationsTestSeeds.ClientRoleClientAsync(factory, "save-integration-forbidden");

        var response = await client.PutAsJsonAsync("/api/admin/integrations/smtp", new
        {
            isEnabled = false,
            config = new Dictionary<string, string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/admin/integrations/smtp", new
        {
            isEnabled = false,
            config = new Dictionary<string, string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
