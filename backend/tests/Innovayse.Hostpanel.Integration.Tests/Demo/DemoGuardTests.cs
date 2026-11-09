using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo guard: a command marked <c>IDemoRestricted</c> is refused with 403
/// <c>DEMO_ACTION_DISABLED</c> in the caller's language while the deployment is a demo, and runs
/// normally when it is not. Driven through <c>PUT /api/admin/settings/{id}</c>, a restricted route.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DemoGuardTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The restricted setting the tests try to change.</summary>
    private const string SettingKey = "portal.template";

    /// <summary>The value the setting is restored to after a successful write.</summary>
    private static readonly string RestoredValue = PortalSettingKeys.TemplateValue(PortalSettingKeys.DefaultTemplate);

    /// <summary>Builds an admin client on the given host and finds the restricted setting's id.</summary>
    /// <param name="host">The host to call.</param>
    /// <param name="language">The <c>X-Language</c> to send, or null for the default.</param>
    /// <returns>The client and the setting id.</returns>
    private async Task<AdminSetting> AdminAndSettingAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string? language = null)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        if (language is not null)
        {
            client.DefaultRequestHeaders.Add("X-Language", language);
        }

        return new(client, await AdminTestSeeds.FindSettingIdAsync(client, SettingKey));
    }

    /// <summary>On a demo deployment the restricted command is refused 403 with the demo code and a sentence.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_RefusesARestrictedCommandAsync()
    {
        var (client, id) = await AdminAndSettingAsync(DemoTestHost.Enabled(factory));

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "nova" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.DemoActionDisabled);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(403);
        body.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>The refusal sentence follows <c>X-Language</c>: Russian and Armenian differ from English and each other.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_TheSentenceFollowsTheRequestLanguageAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        var details = new Dictionary<string, string?>();

        foreach (var language in new[] { "en", "ru", "hy" })
        {
            var (client, id) = await AdminAndSettingAsync(host, language);
            var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "nova" });
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            details[language] = body.RootElement.GetProperty("detail").GetString();
        }

        details.Values.Should().OnlyHaveUniqueItems();
    }

    /// <summary>The guard refuses before any write: the setting keeps its value.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenEnabled_LeavesTheSettingUntouchedAsync()
    {
        var (client, id) = await AdminAndSettingAsync(DemoTestHost.Enabled(factory));
        var before = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");

        await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "nova" });

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
        after.GetProperty("value").GetString().Should().Be(before.GetProperty("value").GetString());
    }

    /// <summary>With the demo off the same command runs exactly as before.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenDisabled_TheSameCommandRunsAsync()
    {
        var (client, id) = await AdminAndSettingAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "nova" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = RestoredValue });
    }
}
