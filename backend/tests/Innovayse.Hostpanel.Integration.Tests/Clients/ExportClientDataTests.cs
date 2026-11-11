using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Audit;
using Innovayse.Hostpanel.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>GET /api/clients/{id}/export</c>: the admin data-export dialog's download.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ExportClientDataTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Exporting only the requested section returns that section and nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportReturnsOnlyTheRequestedSection()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=profileData");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("profileData").GetProperty("id").GetInt32().Should().Be(clientId);
        // ClientExportDto omits an unrequested section from the wire (WhenWritingNull) rather than sending null.
        body.RootElement.TryGetProperty("domains", out _).Should().BeFalse();
    }

    /// <summary>Exporting an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/export?fields=profileData");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Exporting a client's data is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/export?fields=profileData");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "export-forbidden");

        var response = await client.GetAsync($"/api/clients/{clientId}/export?fields=profileData");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The profile section carries the billing preferences, switches, notification flags and registration facts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportProfileCarriesTheWholeProfile()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await ClientSettingsSeeds.SetAsync(factory, clientId, taxExempt: true, disableCcProcessing: true, allowSso: false);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=profileData");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("profileData");
        foreach (var member in new[] { "address", "billing", "marketingOptIn", "registration" })
        {
            profile.TryGetProperty(member, out _).Should().BeTrue($"the profile must carry {member}");
        }

        foreach (var member in new[] { "currency", "creditBalance", "paymentMethod", "billingContact" })
        {
            profile.GetProperty("billing").TryGetProperty(member, out _).Should().BeTrue($"the billing group must carry {member}");
        }

        profile.GetProperty("settings").GetProperty("taxExempt").GetBoolean().Should().BeTrue();
        profile.GetProperty("settings").GetProperty("disableCcProcessing").GetBoolean().Should().BeTrue();
        profile.GetProperty("settings").GetProperty("allowSso").GetBoolean().Should().BeFalse();
        profile.GetProperty("notifications").GetProperty("invoice").GetBoolean().Should().BeTrue();
        profile.GetProperty("registration").TryGetProperty("ipAddress", out _).Should().BeTrue();
    }

    /// <summary>Contacts are exported with their address and notification flags.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportContactsCarryTheirAddressAndNotifyFlags()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Billing", $"c-{Guid.NewGuid():N}@example.test", false))).EnsureSuccessStatusCode();

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=contacts");

        var contact = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contacts").EnumerateArray().Single();
        contact.TryGetProperty("street", out _).Should().BeTrue();
        contact.TryGetProperty("country", out _).Should().BeTrue();
        contact.GetProperty("notifications").GetProperty("invoice").GetBoolean().Should().BeFalse();
        contact.GetProperty("notifications").GetProperty("support").GetBoolean().Should().BeTrue();
    }

    /// <summary>Invitations are exported without their secret token; linked users with their permissions.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportCarriesInvitationsWithoutTheTokenAndLinkedUsers()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var invitee = $"invitee-{Guid.NewGuid():N}@example.test";
        var token = await ClientsTestSeeds.SeedInvitationAsync(factory, clientId, invitee);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=invitations&fields=users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain(token);
        using var body = JsonDocument.Parse(json);
        body.RootElement.GetProperty("invitations").EnumerateArray().Single().GetProperty("email").GetString().Should().Be(invitee);
        body.RootElement.GetProperty("users").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// The email section finds mail addressed to a contact in any letter case, and carries the flag
    /// saying nothing was cut.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportEmailsMatchContactAddressesCaseInsensitively()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var address = $"Mixed-{Guid.NewGuid():N}@Example.Test";
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Mail", address, true))).EnsureSuccessStatusCode();
        await SeedEmailLogsAsync(address.ToUpperInvariant(), 1);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=emails");

        var emails = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("emails");
        emails.GetProperty("items").GetArrayLength().Should().Be(1);
        emails.GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    /// <summary>A long mail history is cut at the limit and the payload says so.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportEmailsStatesTruncation()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var address = $"long-{Guid.NewGuid():N}@example.test";
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Long", address, true))).EnsureSuccessStatusCode();
        await SeedEmailLogsAsync(address, 501);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=emails");

        var emails = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("emails");
        emails.GetProperty("limit").GetInt32().Should().Be(500);
        emails.GetProperty("items").GetArrayLength().Should().Be(500);
        emails.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    /// <summary>The activity log states its limit and that nothing was cut when the history is short.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportActivityLogStatesItsLimit()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.ActivityLogs.Add(ActivityLog.Create(clientId, "Profile Updated", null, "Staff", null, null));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=activityLog");

        var log = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activityLog");
        log.GetProperty("items").GetArrayLength().Should().Be(1);
        log.GetProperty("truncated").GetBoolean().Should().BeFalse();
        log.GetProperty("limit").GetInt32().Should().Be(500);
    }

    /// <summary>Sections with nothing to say are empty arrays or data, never an English sentence.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportEmptySectionsCarryNoSentence()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=payMethods&fields=consentHistory&fields=domains");

        var json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        body.RootElement.GetProperty("domains").GetArrayLength().Should().Be(0);
        body.RootElement.GetProperty("payMethods").GetArrayLength().Should().Be(0);
        body.RootElement.GetProperty("consentHistory").EnumerateArray().Single().GetProperty("granted").GetBoolean().Should().BeFalse();
        json.Should().NotContain("note").And.NotContain("not stored");
    }

    /// <summary>A section key that names nothing is refused with 400 instead of being silently skipped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportRefusesAnUnknownSectionKey()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=profileData&fields=profileDta");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Section keys are matched in any letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportAcceptsSectionKeysInAnyCase()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/export?fields=PROFILEDATA");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Writes mail log lines addressed to one address.</summary>
    /// <param name="to">The address written on every line.</param>
    /// <param name="count">How many lines to write.</param>
    /// <returns>A task that completes once the lines are saved.</returns>
    private async Task SeedEmailLogsAsync(string to, int count)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = SeededAt;
        for (var i = 0; i < count; i++)
        {
            context.EmailLogs.Add(EmailLog.Create(to, $"Subject {i}", "body", null, now.AddSeconds(-i)));
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// An export leaves one activity line for the acting admin that names the sections taken and holds
    /// none of their content; an export that finds no client leaves none.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExportIsAuditedWithTheActingAdmin()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        (await admin.GetAsync($"/api/clients/{clientId}/export?fields=profileData&fields=notes"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/export?fields=profileData"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Exported Client Data");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var line = await db.ActivityLogs.AsNoTracking().SingleAsync(l => l.ClientId == clientId);
        line.Description.Should().Contain("notes").And.Contain("profileData");
        (await db.ActivityLogs.AsNoTracking().AnyAsync(l => l.ClientId == ClientsTestSeeds.NoSuchClientId)).Should().BeFalse();
    }
}
