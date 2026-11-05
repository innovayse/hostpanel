using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>PUT /api/admin/settings/{id}</c>: an admin can change a free-text or a vocabulary-bound
/// setting's value, a row outside the general catalogue is refused <c>SETTING_NOT_EDITABLE</c>, an
/// unknown id is refused <c>SETTING_NOT_FOUND</c>, an out-of-vocabulary or
/// wrong-shape value is refused <c>INVALID_SETTING_VALUE</c> with a per-key message, a caller with
/// only the Client role is refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateSettingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id no seeded or created setting ever takes.</summary>
    private const int NoSuchSettingId = 999_999_999;

    /// <summary>
    /// A row outside the general catalogue is refused <c>SETTING_NOT_EDITABLE</c> (400) and its stored
    /// value is left exactly as it was: integration fields (secret and plain), an integration switch,
    /// a plugin's integration field, the setup token and a deployment key.
    /// </summary>
    /// <param name="key">The full setting key of the row outside the catalogue.</param>
    /// <param name="stored">The value planted under the key before the write.</param>
    [Theory]
    [InlineData("integration:plesk:password", "stored-panel-password")]
    [InlineData("integration:gateway:publishable_key", "pk_test_stored")]
    [InlineData("integration:enom:is_enabled", "false")]
    [InlineData("integration:acme-plugin:endpoint", "https://stored.example.com")]
    [InlineData("auth:setup-token", "stored-setup-token")]
    [InlineData("company.name", "HostPanel")]
    public async Task KeyOutsideTheCatalogue_IsRefusedAndKeepsItsValueAsync(string key, string stored)
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await SecretSettingTestSeeds.StoreAsync(factory, key, stored);
        var before = await SecretSettingTestSeeds.ReadRawAsync(factory, key);

        var update = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "overwritten" });
        var empty = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = string.Empty });

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(update, HostpanelErrorCodes.SETTING_NOT_EDITABLE);
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(empty, HostpanelErrorCodes.SETTING_NOT_EDITABLE);
        (await SecretSettingTestSeeds.ReadRawAsync(factory, key)).Should().Be(before);
    }

    /// <summary>A plugin-system key (<c>plugins.allow_unsigned</c>) is in the catalogue and still updates.</summary>
    [Fact]
    public async Task PluginKey_AllowUnsigned_StillUpdatesAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, PluginSettingKeys.AllowUnsigned);

        try
        {
            (await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = PluginSettingKeys.On })).StatusCode
                .Should().Be(HttpStatusCode.NoContent);
            var read = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
            read.GetProperty("value").GetString().Should().Be(PluginSettingKeys.On);
        }
        finally
        {
            await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = PluginSettingKeys.Off });
        }
    }

    /// <summary>A billing key in the catalogue still updates.</summary>
    [Fact]
    public async Task BillingKey_InvoiceDaysBefore_StillUpdatesAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, BillingSettingKeys.InvoiceDaysBefore);
        var original = (await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}")).GetProperty("value").GetString();

        try
        {
            (await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "14" })).StatusCode
                .Should().Be(HttpStatusCode.NoContent);
            var read = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
            read.GetProperty("value").GetString().Should().Be("14");
        }
        finally
        {
            await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = original });
        }
    }

    /// <summary>A value from the key's own vocabulary is stored, in its canonical spelling.</summary>
    [Fact]
    public async Task TemplateKey_StoresTheCanonicalSpellingAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.template");

        var update = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "NOVA" });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
        read.GetProperty("value").GetString().Should().Be("nova");

        // Leave the deployment's default template as it was for any other test that reads it.
        await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = PortalSettingKeys.TemplateValue(PortalSettingKeys.DefaultTemplate) });
    }

    /// <summary>
    /// A value outside the key's fixed vocabulary answers 400 with the fixed
    /// <c>INVALID_SETTING_VALUE</c> code and the per-key wording under <c>errors[key]</c>.
    /// </summary>
    [Fact]
    public async Task TemplateKey_UnknownValue_RefusesWithDetailAndErrorsAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.template");

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "bootstrap" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVALID_SETTING_VALUE);
        body.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("errors").GetProperty("portal.template")[0].GetString()
            .Should().Contain("nova");
    }

    /// <summary>A shaped free-text key refuses a value with the wrong shape.</summary>
    [Fact]
    public async Task BrandPrimary_WrongShape_RefusesWithDetailAndErrorsAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.brand.primary");

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "not-a-colour" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVALID_SETTING_VALUE);
        body.RootElement.GetProperty("errors").GetProperty("portal.brand.primary")[0].GetString()
            .Should().Contain("#1a73e8");
    }

    /// <summary>The contact email refuses a value that is not an address.</summary>
    [Fact]
    public async Task ContactEmail_NotAnAddress_RefusesWithDetailAndErrorsAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.contact.email");

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "not-an-address" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVALID_SETTING_VALUE);
        body.RootElement.GetProperty("errors").GetProperty("portal.contact.email")[0].GetString()
            .Should().Contain("support@example.com");
    }

    /// <summary>The map address refuses a plain http address.</summary>
    [Fact]
    public async Task ContactMapUrl_Http_RefusesAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.contact.map_url");

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "http://maps.example.com/embed" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A per-language address is free text, stored trimmed like the default one.</summary>
    [Fact]
    public async Task ContactAddressRu_StoresTrimmedFreeTextAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.contact.address.ru");

        var update = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "  ул. Примерная, 1  " });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
        read.GetProperty("value").GetString().Should().Be("ул. Примерная, 1");
    }

    /// <summary>Free text with no vocabulary or shape is stored trimmed, unchanged otherwise.</summary>
    [Fact]
    public async Task SiteName_StoresTrimmedFreeTextAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.site.name");

        var update = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "  Acme Hosting  " });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
        read.GetProperty("value").GetString().Should().Be("Acme Hosting");

        // Leave the deployment's site name as it was for any other test that reads it.
        await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "Acme Hosting" });
    }

    /// <summary>A billing key refuses a value outside its pattern with INVALID_SETTING_VALUE.</summary>
    [Fact]
    public async Task BillingKey_BadValue_IsRefusedAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, BillingSettingKeys.InvoiceDaysBefore);

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "abc" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVALID_SETTING_VALUE);
        body.RootElement.GetProperty("errors").GetProperty(BillingSettingKeys.InvoiceDaysBefore)[0].GetString()
            .Should().Contain("1 to 90");
    }

    /// <summary>A zero lead time is refused: invoice_days_before starts at 1, unlike the overdue grace.</summary>
    [Fact]
    public async Task InvoiceDaysBefore_Zero_IsRefusedAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, BillingSettingKeys.InvoiceDaysBefore);

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "0" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVALID_SETTING_VALUE);
        body.RootElement.GetProperty("errors").GetProperty(BillingSettingKeys.InvoiceDaysBefore)[0].GetString()
            .Should().Contain("a whole number of days from 1 to 90");
    }

    /// <summary>Clearing the reminder list is allowed (switches reminders off).</summary>
    [Fact]
    public async Task ReminderDays_CanBeClearedAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, BillingSettingKeys.ReminderDays);

        var update = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = string.Empty });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
        read.GetProperty("value").GetString().Should().BeEmpty();

        // Restore the seeded default for any other test that reads it.
        await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "7,3,1" });
    }

    /// <summary>Updating an unknown id answers 404 with <c>SETTING_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task UnknownId_RefusesNotFoundAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{NoSuchSettingId}", new { value = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SETTING_NOT_FOUND);
    }

    /// <summary>A social link refuses anything but an https address.</summary>
    [Fact]
    public async Task SocialLink_NotHttps_RefusesAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, PortalSettingKeys.SocialFacebook);

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "javascript:alert(1)" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVALID_SETTING_VALUE);
    }

    /// <summary>
    /// Replacing a branding image removes the upload the old value pointed at, and keeps the new one.
    /// </summary>
    [Fact]
    public async Task LogoReplaced_RemovesTheOldUploadAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, PortalSettingKeys.LogoMark);
        var first = await UploadLogoMarkAsync(client);
        var second = await UploadLogoMarkAsync(client);

        (await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = first })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = second })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        File.Exists(DiskPathOf(first)).Should().BeFalse();
        File.Exists(DiskPathOf(second)).Should().BeTrue();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "update-setting-forbidden");

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{NoSuchSettingId}", new { value = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A caller with no credential is refused with 401.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/settings/{NoSuchSettingId}", new { value = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Uploads a one-pixel logo mark and returns the URL it was stored under.</summary>
    /// <param name="client">An admin client.</param>
    /// <returns>The stored file's root-relative URL.</returns>
    private static async Task<string> UploadLogoMarkAsync(HttpClient client)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "mark.png");

        var response = await client.PostAsync("/api/admin/settings/branding/logomark", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("url").GetString()!;
    }

    /// <summary>Maps a stored branding URL to the file on the host's disk.</summary>
    /// <param name="url">The root-relative URL.</param>
    /// <returns>The absolute file path.</returns>
    private string DiskPathOf(string url)
    {
        var env = factory.Services.GetRequiredService<IWebHostEnvironment>();
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, url.TrimStart('/'));
    }
}
