using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/settings/public/legal/{page}</c>: the operator's legal text of one page is readable by a caller
/// with no credential, carries only that page's rows that have a value, and is not part of the public settings
/// that ride in every page.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPublicLegalTextTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// The page answers anonymously with the rows the operator wrote for it, never another page's, while the
    /// global public read does not carry any legal text.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServesOnlyTheRequestedPage_AndKeepsLegalTextOutOfThePublicSettingsAsync()
    {
        var admin = await AdminTestSeeds.AdminClientAsync(factory);
        var refundId = await AdminTestSeeds.FindSettingIdAsync(admin, PortalSettingKeys.ForLanguage(PortalSettingKeys.LegalRefund, "hy"));
        var privacyId = await AdminTestSeeds.FindSettingIdAsync(admin, PortalSettingKeys.LegalPrivacy);
        await admin.PutAsJsonAsync($"/api/admin/settings/{refundId}", new { value = "## Refunds\n\nWithin 14 days." });
        await admin.PutAsJsonAsync($"/api/admin/settings/{privacyId}", new { value = "We keep little data." });

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/settings/public/legal/refund");
        var global = await anonymous.GetFromJsonAsync<List<JsonElement>>("/api/settings/public");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows!.Should().ContainSingle();
        rows![0].GetProperty("key").GetString().Should().Be(PortalSettingKeys.ForLanguage(PortalSettingKeys.LegalRefund, "hy"));
        rows[0].GetProperty("value").GetString().Should().Be("## Refunds\n\nWithin 14 days.");
        global!.Select(row => row.GetProperty("key").GetString()!).Where(PortalSettingKeys.IsLegalText).Should().BeEmpty();

        await admin.PutAsJsonAsync($"/api/admin/settings/{refundId}", new { value = string.Empty });
        await admin.PutAsJsonAsync($"/api/admin/settings/{privacyId}", new { value = string.Empty });
    }

    /// <summary>A page the operator wrote nothing for answers an empty list, so the storefront keeps the built-in text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PageWithNoText_AnswersAnEmptyListAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/settings/public/legal/cookies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<JsonElement>>()).Should().BeEmpty();
    }

    /// <summary>A name that is not a legal page, including a settings key, answers 404 <c>SETTING_NOT_FOUND</c>.</summary>
    /// <param name="page">The requested page segment.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("unknown")]
    [InlineData("portal.legal.terms")]
    [InlineData("company.name")]
    public async Task UnknownPage_IsRefusedAsync(string page)
    {
        var response = await factory.CreateClient().GetAsync($"/api/settings/public/legal/{page}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SETTING_NOT_FOUND);
    }
}
