using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/domain-renewal-emails</c> against Postgres: every matching renewal
/// reminder, most recently sent first.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDomainRenewalEmailsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A sent renewal reminder appears in the report, scoped to its client, with its type by name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsASentReminder()
    {
        var marker = $"Renew{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var domain = RegisteredDomain.Register(
                clientId, $"{marker}.com", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            domain.AddReminder(DomainReminderType.ThirtyDaysBeforeExpiry, $"{marker}@reports.test", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            db.Domains.Add(domain);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/domain-renewal-emails?clientId={clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Domain names are stored in lower case.
        body.GetProperty("rows").EnumerateArray().Should().Contain(
            r => r.GetProperty("domain").GetString() == $"{marker}.com".ToLowerInvariant()
                && r.GetProperty("reminder").GetString() == nameof(DomainReminderType.ThirtyDaysBeforeExpiry));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/domain-renewal-emails");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "domain-renewal-emails");

        var response = await client.GetAsync("/api/reports/domain-renewal-emails");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
