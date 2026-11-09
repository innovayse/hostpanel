using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/invoices/export</c> against Postgres: the invoices report as a CSV file.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ExportInvoicesReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The Invoices CSV export answers with the expected headers, content type and filename.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTheExpectedCsvShape()
    {
        var marker = $"ExportInv{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 0m, 15m);

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/invoices/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("invoices-report.csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith(
            "ID,Client ID,Client Name,Status,Created Date,Due Date,Date Paid,Currency,SubTotal,Credit,Tax,Tax Rate,Total,Payment Method,Notes");
        csv.Should().Contain(marker);
        csv.Split("\r\n").Single(l => l.Contains(marker, StringComparison.Ordinal)).Should().Contain(",USD,");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/invoices/export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "export-invoices-report");

        var response = await client.GetAsync("/api/reports/invoices/export");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
