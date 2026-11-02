using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Audit;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>PUT /api/admin/settings/{id}</c> for the plugin system's setting "allow unsigned plugins": it accepts only
/// <c>true</c> or <c>false</c>, and every change of it is written to the activity log with the old and the new value,
/// because it weakens a security control. A write that changes nothing is not logged.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdatePluginSettingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The audit line begins like this for the setting.</summary>
    private const string AuditPrefix = "Changed Setting - Setting: plugins.allow_unsigned";

    /// <summary>Turning the setting on and off again writes one system-wide audit line per change, naming both values.</summary>
    [Fact]
    public async Task EveryChangeOfAllowUnsigned_IsWrittenToTheActivityLogAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, PluginSettingKeys.AllowUnsigned);
        var before = await AuditLinesAsync();

        try
        {
            (await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "TRUE" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "false" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var lines = (await AuditLinesAsync()).Skip(before.Count).ToList();
            lines.Should().HaveCount(2);
            lines[0].Description.Should().Be($"{AuditPrefix} - From: false - To: true");
            lines[1].Description.Should().Be($"{AuditPrefix} - From: true - To: false");
            lines.Should().OnlyContain(l => l.ClientId == null, "a security setting concerns no client");
        }
        finally
        {
            await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = PluginSettingKeys.Off });
        }
    }

    /// <summary>Saving the value the setting already has changes nothing and so writes no audit line.</summary>
    [Fact]
    public async Task SavingTheSameValue_WritesNoAuditLineAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, PluginSettingKeys.AllowUnsigned);
        var before = await AuditLinesAsync();

        (await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = PluginSettingKeys.Off })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AuditLinesAsync()).Should().HaveCount(before.Count);
    }

    /// <summary>Any value but <c>true</c> or <c>false</c> is refused with <c>INVALID_SETTING_VALUE</c> and nothing is logged.</summary>
    [Fact]
    public async Task AValueThatIsNotABoolean_IsRefusedAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, PluginSettingKeys.AllowUnsigned);
        var before = await AuditLinesAsync();

        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value = "maybe" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVALID_SETTING_VALUE);
        (await AuditLinesAsync()).Should().HaveCount(before.Count);
        var read = await client.GetFromJsonAsync<JsonElement>($"/api/admin/settings/{id}");
        read.GetProperty("value").GetString().Should().Be(PluginSettingKeys.Off);
    }

    /// <summary>The audit lines written for the setting so far, oldest first.</summary>
    /// <returns>The activity log lines.</returns>
    private async Task<List<ActivityLog>> AuditLinesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ActivityLogs.AsNoTracking()
            .Where(l => l.Description.StartsWith(AuditPrefix))
            .OrderBy(l => l.Id)
            .ToListAsync();
    }
}
