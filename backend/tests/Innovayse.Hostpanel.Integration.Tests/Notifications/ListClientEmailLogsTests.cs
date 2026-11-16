using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Notifications.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>ListClientEmailLogs</c> use case, reachable both through the
/// admin per-client route (<c>GET /api/admin/email-logs/client/{clientId}</c>) and the client
/// portal's own list (<c>GET /api/me/emails</c>).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientEmailLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client id that names nobody answers an empty page, not a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_UnknownClient_ReturnsAnEmptyPage()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/email-logs/client/999999?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("totalCount").GetInt32().Should().Be(0);
        page.GetProperty("pageNumber").GetInt32().Should().Be(1);
        page.GetProperty("totalPages").GetInt32().Should().Be(0);
    }

    /// <summary>The admin per-client route finds a log seeded straight at the client's own address, and lists nobody else's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_FindsTheClientsOwnLog()
    {
        var (_, clientId, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        await NotificationsTestSeeds.SeedLogAsync(factory, email, "Hello, client");

        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"/api/admin/email-logs/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        // Registering mailed the client a welcome as well (delivered asynchronously, so it may or may
        // not be listed yet): the route's contract is that everything it lists is addressed to this
        // client, and that the seeded row is among it.
        var items = page.GetProperty("items").EnumerateArray().ToList();
        page.GetProperty("totalCount").GetInt32().Should().Be(items.Count);
        items.Should().OnlyContain(i => i.GetProperty("to").GetString() == email);
        items.Select(i => i.GetProperty("subject").GetString()).Should().Contain("Hello, client");
    }

    /// <summary>
    /// The search, the outcome and the period narrow the client's mail together and never reach another address; the last day of
    /// the period is kept whole and the total counts every match before paging.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_SearchOutcomeAndPeriodNarrowTheClientsMail()
    {
        var (_, clientId, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var day = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var first = await NotificationsTestSeeds.SeedLogAsync(factory, email, $"Invoice {token}", sentAt: day);
        var last = await NotificationsTestSeeds.SeedLogAsync(factory, email, $"Invoice {token}", EmailSendFailure.Timeout, day.AddDays(1).AddSeconds(-1));
        await NotificationsTestSeeds.SeedLogAsync(factory, email, $"Invoice {token}", sentAt: day.AddDays(1));
        await NotificationsTestSeeds.SeedLogAsync(factory, email, $"Receipt {token}", sentAt: day);
        await NotificationsTestSeeds.SeedLogAsync(factory, $"other-{token}@example.test", $"Invoice {token}", sentAt: day);
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);
        var route = $"/api/admin/email-logs/client/{clientId}?page=1&pageSize=100&search=invoice%20{token.ToUpperInvariant()}";

        var oneDay = await PagedListReads.PageAsync(admin, $"{route}&from=2026-03-10&to=2026-03-10");
        var failedOnly = await PagedListReads.PageAsync(admin, $"{route}&from=2026-03-10&to=2026-03-10&success=false");
        var deliveredOnly = await PagedListReads.PageAsync(admin, $"{route}&success=true");
        var paged = await PagedListReads.PageAsync(admin, $"/api/admin/email-logs/client/{clientId}?page=2&pageSize=1&search={token}&from=2026-03-10&to=2026-03-10");

        PagedListReads.IdsOf(oneDay).Should().BeEquivalentTo([first, last]);
        PagedListReads.IdsOf(failedOnly).Should().Equal(last);
        PagedListReads.IdsOf(deliveredOnly).Should().HaveCount(2).And.Contain(first);
        paged.GetProperty("totalCount").GetInt32().Should().Be(3);
        PagedListReads.IdsOf(paged).Should().HaveCount(1);
    }

    /// <summary>A percent sign typed into the search matches itself on a client's mail, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_SearchWildcardsAreLiteral()
    {
        var (_, clientId, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        var token = PagedListReads.UniqueToken();
        var percent = await NotificationsTestSeeds.SeedLogAsync(factory, email, $"{token} 50% off");
        await NotificationsTestSeeds.SeedLogAsync(factory, email, $"{token} 50x off");
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var page = await PagedListReads.PageAsync(admin, $"/api/admin/email-logs/client/{clientId}?page=1&pageSize=100&search={token}%2050%25");

        PagedListReads.IdsOf(page).Should().Equal(percent);
    }

    /// <summary>A page outside the limits or a period that ends before it starts is refused with 400 on that field.</summary>
    /// <param name="queryString">The query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    [InlineData("page=1&pageSize=20&from=2026-03-11&to=2026-03-10", "to")]
    public async Task AdminListByClient_ValueOutOfRangeIsRefused(string queryString, string field)
    {
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/email-logs/client/1?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>The admin per-client route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/email-logs/client/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client reading their own mail sees only their own address, never somebody else's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyEmails_List_ReturnsOwnLogsOnly()
    {
        var (http, _, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        await NotificationsTestSeeds.SeedLogAsync(factory, email, "Mine");
        await NotificationsTestSeeds.SeedLogAsync(factory, $"someone-else-{Guid.NewGuid():N}@example.test", "Not mine");

        var response = await http.GetAsync("/api/me/emails?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var subjects = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("subject").GetString()).ToList();
        subjects.Should().Contain("Mine").And.NotContain("Not mine");
    }

    /// <summary>The client portal route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyEmails_List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
