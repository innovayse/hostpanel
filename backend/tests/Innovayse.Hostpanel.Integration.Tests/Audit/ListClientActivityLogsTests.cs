using System.Net;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Audit;
using Microsoft.Extensions.DependencyInjection;
using static Innovayse.Hostpanel.Integration.Tests.Fixtures.PagedListReads;

namespace Innovayse.Hostpanel.Integration.Tests.Audit;

/// <summary>
/// <c>GET /api/clients/{id}/activity-logs</c> against Postgres: the activity log of one client, searched over the admin,
/// the description and the IP address, narrowed by a period, ordered newest first and paged on the server. This query is the
/// module's only use case.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientActivityLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client id no seeded or created client ever takes.</summary>
    private const int NoSuchClientId = 999_999_999;

    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Results come back newest first, and the id breaks a tie so the order is stable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrdersNewestFirstAndTheIdBreaksATie()
    {
        var (admin, clientId) = await AdminAndClientAsync();
        var oldest = await SeedLogAsync(clientId, "Oldest", SeededAt.AddDays(-2));
        var tiedFirst = await SeedLogAsync(clientId, "Tied first", SeededAt);
        var tiedSecond = await SeedLogAsync(clientId, "Tied second", SeededAt);
        var middle = await SeedLogAsync(clientId, "Middle", SeededAt.AddDays(-1));

        var page = await PageAsync(admin, Route(clientId, string.Empty));

        IdsOf(page).Should().Equal(tiedSecond, tiedFirst, middle, oldest);
    }

    /// <summary>The search matches the admin name, the admin email, the description and the IP address in any letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsAdminDescriptionAndIpInAnyCase()
    {
        var (admin, clientId) = await AdminAndClientAsync();
        var token = UniqueToken();
        var byName = await SeedLogAsync(clientId, "By name", SeededAt, adminName: $"Ada {token}");
        var byEmail = await SeedLogAsync(clientId, "By email", SeededAt, adminEmail: $"{token}@example.test");
        var byDescription = await SeedLogAsync(clientId, $"Added Invoice {token}", SeededAt);
        var byIp = await SeedLogAsync(clientId, "By IP", SeededAt, ipAddress: $"10.0.0.5 {token}");
        await SeedLogAsync(clientId, "Unrelated", SeededAt, adminName: "Grace Hopper");

        var page = await PageAsync(admin, Route(clientId, $"search={token.ToUpperInvariant()}"));

        IdsOf(page).Should().BeEquivalentTo([byName, byEmail, byDescription, byIp]);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var (admin, clientId) = await AdminAndClientAsync();
        var token = UniqueToken();
        var percent = await SeedLogAsync(clientId, $"{token} 50% off", SeededAt);
        await SeedLogAsync(clientId, $"{token} 50x off", SeededAt);
        var underscore = await SeedLogAsync(clientId, $"{token} a_c", SeededAt);
        await SeedLogAsync(clientId, $"{token} abc", SeededAt);

        var percentPage = await PageAsync(admin, Route(clientId, $"search={token}%2050%25"));
        var underscorePage = await PageAsync(admin, Route(clientId, $"search={token}%20a_c"));
        var bareWildcard = await PageAsync(admin, Route(clientId, $"search={token}%25%25"));

        IdsOf(percentPage).Should().Equal(percent);
        IdsOf(underscorePage).Should().Equal(underscore);
        IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>The period keeps the whole of its first and last day, and nothing outside them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PeriodKeepsTheWholeLastDay()
    {
        var (admin, clientId) = await AdminAndClientAsync();
        var day = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        await SeedLogAsync(clientId, "Before", day.AddSeconds(-1));
        var first = await SeedLogAsync(clientId, "First instant", day);
        var last = await SeedLogAsync(clientId, "Last second", day.AddDays(1).AddSeconds(-1));
        await SeedLogAsync(clientId, "After", day.AddDays(1));

        var oneDay = await PageAsync(admin, Route(clientId, "from=2026-03-10&to=2026-03-10"));
        var fromOnly = await PageAsync(admin, Route(clientId, "from=2026-03-10"));
        var toOnly = await PageAsync(admin, Route(clientId, "to=2026-03-09"));

        IdsOf(oneDay).Should().BeEquivalentTo([first, last]);
        IdsOf(fromOnly).Should().HaveCount(3);
        IdsOf(toOnly).Should().HaveCount(1);
    }

    /// <summary>The search and the period narrow together: an entry must pass both.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchAndPeriodNarrowTogether()
    {
        var (admin, clientId) = await AdminAndClientAsync();
        var token = UniqueToken();
        var match = await SeedLogAsync(clientId, $"Payment {token}", SeededAt);
        await SeedLogAsync(clientId, $"Payment {token}", SeededAt.AddDays(3));
        await SeedLogAsync(clientId, "Other", SeededAt);

        var page = await PageAsync(admin, Route(clientId, $"search={token}&from=2026-03-10&to=2026-03-10"));

        IdsOf(page).Should().Equal(match);
    }

    /// <summary>An entry belonging to a different client is never returned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NeverReturnsAnotherClientsEntry()
    {
        var (admin, clientId) = await AdminAndClientAsync();
        var otherClientId = await SeedClientAsync();
        var mine = await SeedLogAsync(clientId, "Mine", SeededAt);
        await SeedLogAsync(otherClientId, "Theirs", SeededAt);

        var page = await PageAsync(admin, Route(clientId, string.Empty));

        IdsOf(page).Should().Equal(mine);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var (admin, clientId) = await AdminAndClientAsync();
        var token = UniqueToken();
        var oldest = await SeedLogAsync(clientId, $"One {token}", SeededAt);
        var middle = await SeedLogAsync(clientId, $"Two {token}", SeededAt.AddMinutes(1));
        var newest = await SeedLogAsync(clientId, $"Three {token}", SeededAt.AddMinutes(2));
        await SeedLogAsync(clientId, "Not a match", SeededAt.AddMinutes(3));
        var route = $"/api/clients/{clientId}/activity-logs?pageSize=2&search={token}";

        var first = await PageAsync(admin, $"{route}&page=1");
        var second = await PageAsync(admin, $"{route}&page=2");
        var beyond = await PageAsync(admin, $"{route}&page=3");

        IdsOf(first).Should().Equal(newest, middle);
        first.GetProperty("totalCount").GetInt32().Should().Be(3);
        IdsOf(second).Should().Equal(oldest);
        IdsOf(beyond).Should().BeEmpty();
    }

    /// <summary>A client with no recorded activity, or an id that names no client, answers 200 with an empty page.</summary>
    /// <param name="useUnknownClient">Whether to ask for a client id that names nobody.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientWithNoActivityAnswersAnEmptyPage(bool useUnknownClient)
    {
        var (admin, clientId) = await AdminAndClientAsync();

        var page = await PageAsync(admin, Route(useUnknownClient ? NoSuchClientId : clientId, string.Empty));

        IdsOf(page).Should().BeEmpty();
        page.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    /// <summary>A page below one, no page, or a page size outside the limit is refused with 400 on the page and page size, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{NoSuchClientId}/activity-logs?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit, or a period that ends before it starts, is refused with 400 on that field.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("search=TOO_LONG", "search")]
    [InlineData("from=2026-03-11&to=2026-03-10", "to")]
    public async Task FilterValueOutOfRangeIsRefused(string filter, string field)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var value = filter.Replace("TOO_LONG", new string('a', PagingLimits.MaxSearchLength + 1), StringComparison.Ordinal);

        var response = await admin.GetAsync($"/api/clients/{NoSuchClientId}/activity-logs?page=1&pageSize=20&{value}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/clients/1/activity-logs?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The address of the first page of a client's log, with the filters appended.</summary>
    /// <param name="clientId">The client whose log is read.</param>
    /// <param name="filters">The filters as a query string tail; empty for none.</param>
    /// <returns>The route with a page of a hundred rows.</returns>
    private static string Route(int clientId, string filters)
    {
        return $"/api/clients/{clientId}/activity-logs?page=1&pageSize=100{(filters.Length == 0 ? string.Empty : "&" + filters)}";
    }

    /// <summary>An admin client and a fresh client to read the log of.</summary>
    /// <returns>The admin client and the new client's id.</returns>
    private async Task<(HttpClient Admin, int ClientId)> AdminAndClientAsync()
    {
        return (await SupportTestSeeds.AdminClientAsync(factory), await SeedClientAsync());
    }

    /// <summary>Seeds a client the log entries can be filed under.</summary>
    /// <returns>The new client's id.</returns>
    private async Task<int> SeedClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await DomainsTestSeeds.SeedClientAsync(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
    }

    /// <summary>Adds a log entry for a client, stamped with the moment given.</summary>
    /// <param name="clientId">The client the row is filed under.</param>
    /// <param name="description">The entry's description.</param>
    /// <param name="createdAt">The moment the entry is stamped with.</param>
    /// <param name="adminName">Optional admin display name.</param>
    /// <param name="adminEmail">Optional admin email.</param>
    /// <param name="ipAddress">Optional IP address.</param>
    /// <returns>The new entry's id.</returns>
    private async Task<int> SeedLogAsync(
        int clientId, string description, DateTimeOffset createdAt,
        string? adminName = null, string? adminEmail = null, string? ipAddress = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var log = ActivityLog.Create(clientId, description, adminId: null, adminName, adminEmail, ipAddress);
        log.Backdate(createdAt);
        context.ActivityLogs.Add(log);
        await context.SaveChangesAsync(CancellationToken.None);
        return log.Id;
    }
}
