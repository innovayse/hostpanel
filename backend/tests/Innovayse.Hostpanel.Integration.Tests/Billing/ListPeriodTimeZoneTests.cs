namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The billing lists' date range, run on a host whose local zone is not UTC. The admin sends bare days, and the last day
/// must stay in whatever zone the host runs in: a day is a UTC day, never a day cut in the host's zone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
[Collection(ProcessTimeZoneCollection.Name)]
public sealed class ListPeriodTimeZoneTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The invoice list keeps the whole last day, up to its last second, on a host east and west of UTC.</summary>
    /// <param name="zoneId">The IANA id of the zone the host runs in.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Asia/Yerevan")]
    [InlineData("America/Los_Angeles")]
    public async Task InvoicePeriodKeepsTheWholeLastDayInAnyHostZoneAsync(string zoneId)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var before = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 9, 23, 59, 59, TimeSpan.Zero));
        var firstInstant = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 10, 0, 0, 0, TimeSpan.Zero));
        var lastInstant = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 10, 23, 59, 59, TimeSpan.Zero));
        var after = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 11, 0, 0, 0, TimeSpan.Zero));

        using var zone = new ProcessTimeZone(zoneId);
        var page = await PagedListReads.PageAsync(
            admin, $"/api/billing?page=1&pageSize=100&clientId={clientId}&from=2020-03-10&to=2020-03-10");

        var ids = PagedListReads.IdsOf(page);
        ids.Should().BeEquivalentTo([firstInstant, lastInstant]);
        ids.Should().NotContain([before, after]);
    }

    /// <summary>The transaction list keeps the whole last day, up to its last second, on a host east and west of UTC.</summary>
    /// <param name="zoneId">The IANA id of the zone the host runs in.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Asia/Yerevan")]
    [InlineData("America/Los_Angeles")]
    public async Task TransactionPeriodKeepsTheWholeLastDayInAnyHostZoneAsync(string zoneId)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var currency = (await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=1&clientId={clientId}"))
            .GetProperty("currency").GetString()!;
        var day = new DateTimeOffset(2020, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var before = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day.AddSeconds(-1), "Before");
        var first = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day, "First instant");
        var last = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day.AddDays(1).AddSeconds(-1), "Last second");
        var after = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day.AddDays(1), "After");

        using var zone = new ProcessTimeZone(zoneId);
        var page = await PagedListReads.PageAsync(
            admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&from=2020-03-10&to=2020-03-10");

        var ids = PagedListReads.IdsOf(page.GetProperty("transactions"));
        ids.Should().BeEquivalentTo([first, last]);
        ids.Should().NotContain([before, after]);
    }
}
