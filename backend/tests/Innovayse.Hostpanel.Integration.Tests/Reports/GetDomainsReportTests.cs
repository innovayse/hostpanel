using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/domains</c> against Postgres: the paginated domains report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDomainsReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A freshly registered domain appears in the paginated domains report with its client's name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsTheClientNameFromTheClientRowItself()
    {
        var marker = $"Domain{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var domain = RegisteredDomain.Register(
                clientId, $"{marker}.com", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            db.Domains.Add(domain);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/domains?page=1&pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The stored Name already carries the TLD ("marker.com", in lower case like every stored domain
        // name), so the report shows it as stored and does not append the TLD a second time. The client
        // name must come from the Client row itself, not from a join against Identity's Users table.
        body.GetProperty("items").EnumerateArray().Should().Contain(
            i => i.GetProperty("domainName").GetString() == $"{marker}.com".ToLowerInvariant()
                && i.GetProperty("clientName").GetString()!.Contains(marker));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/domains?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "domains-report");

        var response = await client.GetAsync("/api/reports/domains?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A page number below one is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetDomainsReport_PageBelowOne_Returns400ValidationError()
    {
        var admin = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/reports/domains?page=0&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReportsTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
