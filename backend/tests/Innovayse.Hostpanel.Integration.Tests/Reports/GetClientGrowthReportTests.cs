using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/admin/reports/client-growth</c>: an admin reads the daily client
/// growth items for a date range, a caller with only the Client role is refused, and the route is
/// closed to a caller with no credential. This use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientGrowthReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads the client-growth report for a well-formed range.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadTheReportAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var from = Uri.EscapeDataString(TestMoments.Seeded.AddDays(-30).ToString("O"));
        var to = Uri.EscapeDataString(TestMoments.Seeded.ToString("O"));

        var response = await client.GetAsync($"/api/admin/reports/client-growth?startDate={from}&endDate={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A sign-up is counted on its UTC day, and the window is half-open: a window ending at the
    /// first instant of that day leaves it out.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsSignUpsPerDayInsideAHalfOpenWindowAsync()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"Growth{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var signedUp = await context.Clients.FindAsync([clientId], CancellationToken.None);
            signedUp!.Backdate(new DateTimeOffset(2030, 3, 5, 12, 0, 0, TimeSpan.Zero));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var start = Uri.EscapeDataString(new DateTimeOffset(2030, 3, 1, 0, 0, 0, TimeSpan.Zero).ToString("O"));
        var afterTheDay = Uri.EscapeDataString(new DateTimeOffset(2030, 3, 6, 0, 0, 0, TimeSpan.Zero).ToString("O"));
        var atTheDay = Uri.EscapeDataString(new DateTimeOffset(2030, 3, 5, 0, 0, 0, TimeSpan.Zero).ToString("O"));

        var counted = await client.GetFromJsonAsync<List<JsonElement>>($"/api/admin/reports/client-growth?startDate={start}&endDate={afterTheDay}");
        var leftOut = await client.GetFromJsonAsync<List<JsonElement>>($"/api/admin/reports/client-growth?startDate={start}&endDate={atTheDay}");

        counted!.Select(r => (r.GetProperty("date").GetString(), r.GetProperty("newClients").GetInt32()))
            .Should().Equal(("2030-03-05", 1));
        leftOut.Should().BeEmpty();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "client-growth-forbidden");

        var response = await client.GetAsync("/api/admin/reports/client-growth");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The client-growth report is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/reports/client-growth");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
