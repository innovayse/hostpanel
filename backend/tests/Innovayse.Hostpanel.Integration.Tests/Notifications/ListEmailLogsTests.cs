using System.Net;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using static Innovayse.Hostpanel.Integration.Tests.Fixtures.PagedListReads;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>ListEmailLogs</c> use case: <c>GET /api/admin/email-logs</c>, the paged, searchable
/// platform log. Every test scopes its rows with a token in the subject, because the log is shared with the mail other
/// tests send.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListEmailLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A log written straight to the database shows up in the admin paged list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_ShowsASeededLog()
    {
        var token = UniqueToken();
        var id = await NotificationsTestSeeds.SeedLogAsync(factory, $"flat-list-{token}@example.test", $"Flat list entry {token}");
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var page = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}");

        IdsOf(page).Should().Equal(id);
        page.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    /// <summary>The search matches the subject and the recipient in any letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_FindsTheSubjectAndTheRecipientInAnyCase()
    {
        var token = UniqueToken();
        var bySubject = await NotificationsTestSeeds.SeedLogAsync(factory, "someone@example.test", $"Invoice {token}");
        var byRecipient = await NotificationsTestSeeds.SeedLogAsync(factory, $"{token}@example.test", "Welcome");
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var page = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token.ToUpperInvariant()}");

        IdsOf(page).Should().BeEquivalentTo([bySubject, byRecipient]);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_WildcardsAreLiteral()
    {
        var token = UniqueToken();
        var percent = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"{token} 50% off");
        await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"{token} 50x off");
        var underscore = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"{token} a_c");
        await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"{token} abc");
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var percentPage = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}%2050%25");
        var underscorePage = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}%20a_c");
        var bareWildcard = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}%25%25");

        IdsOf(percentPage).Should().Equal(percent);
        IdsOf(underscorePage).Should().Equal(underscore);
        IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>The outcome filter keeps the delivered mail or the failed mail.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SuccessFilter_KeepsDeliveredOrFailedMail()
    {
        var token = UniqueToken();
        var delivered = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Delivered {token}");
        var failed = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Failed {token}", EmailSendFailure.Timeout);
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var deliveredPage = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}&success=true");
        var failedPage = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}&success=false");
        var both = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}");

        IdsOf(deliveredPage).Should().Equal(delivered);
        IdsOf(failedPage).Should().Equal(failed);
        IdsOf(both).Should().BeEquivalentTo([delivered, failed]);
    }

    /// <summary>The period keeps the whole of its first and last day, and nothing outside them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Period_KeepsTheWholeLastDay()
    {
        var token = UniqueToken();
        var day = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Before {token}", sentAt: day.AddSeconds(-1));
        var first = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"First {token}", sentAt: day);
        var last = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Last {token}", sentAt: day.AddDays(1).AddSeconds(-1));
        await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"After {token}", sentAt: day.AddDays(1));
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var oneDay = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}&from=2026-03-10&to=2026-03-10");
        var fromOnly = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}&from=2026-03-10");
        var toOnly = await PageAsync(admin, $"/api/admin/email-logs?page=1&pageSize=100&search={token}&to=2026-03-09");

        IdsOf(oneDay).Should().BeEquivalentTo([first, last]);
        IdsOf(fromOnly).Should().HaveCount(3);
        IdsOf(toOnly).Should().HaveCount(1);
    }

    /// <summary>A filter is applied before paging: the total counts every match, each page holds its slice, newest first and the id breaks a tie.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Filter_IsAppliedBeforePaging()
    {
        var token = UniqueToken();
        var oldest = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Oldest {token}", sentAt: SeededAt);
        var tiedFirst = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Tied first {token}", sentAt: SeededAt.AddDays(1));
        var tiedSecond = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Tied second {token}", sentAt: SeededAt.AddDays(1));
        var newest = await NotificationsTestSeeds.SeedLogAsync(factory, "a@example.test", $"Newest {token}", sentAt: SeededAt.AddDays(2));
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);
        var route = $"/api/admin/email-logs?pageSize=3&search={token}";

        var first = await PageAsync(admin, $"{route}&page=1");
        var second = await PageAsync(admin, $"{route}&page=2");
        var beyond = await PageAsync(admin, $"{route}&page=3");

        IdsOf(first).Should().Equal(newest, tiedSecond, tiedFirst);
        first.GetProperty("totalCount").GetInt32().Should().Be(4);
        IdsOf(second).Should().Equal(oldest);
        IdsOf(beyond).Should().BeEmpty();
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
    public async Task Paging_OutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/email-logs?{queryString}");

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
    public async Task Filter_ValueOutOfRangeIsRefused(string filter, string field)
    {
        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);
        var value = filter.Replace("TOO_LONG", new string('a', PagingLimits.MaxSearchLength + 1), StringComparison.Ordinal);

        var response = await admin.GetAsync($"/api/admin/email-logs?page=1&pageSize=20&{value}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/email-logs?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
