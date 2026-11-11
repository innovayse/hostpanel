using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/clients/{clientId}/billable-items/time-billing</c> against Postgres: batch
/// creating one billable item per timesheet entry.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateTimeBillingEntriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating time billing entries produces one billable item per entry.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateTimeBillingEntriesCreatesOneItemPerEntry()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items/time-billing", new
        {
            entries = new[]
            {
                new { description = "Hour 1", hours = 2m, rate = 50m },
                new { description = "Hour 2", hours = 1m, rate = 50m },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ids = await response.Content.ReadFromJsonAsync<int[]>();
        ids.Should().HaveCount(2);
    }

    /// <summary>Creating time billing entries is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateTimeBillingEntriesRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/clients/1/billable-items/time-billing", new
        {
            entries = new[] { new { description = "Hour 1", hours = 1m, rate = 50m } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Time billing entries for a client that does not exist answer 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateTimeBillingEntriesRefusesAnUnknownClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{BillingTestSeeds.NoSuchId}/billable-items/time-billing", new
        {
            entries = new[] { new { description = "Hour 1", hours = 1m, rate = 50m } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
