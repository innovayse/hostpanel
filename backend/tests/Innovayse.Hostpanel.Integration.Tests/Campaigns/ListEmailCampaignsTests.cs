using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>ListEmailCampaigns</c> use case: <c>GET /api/admin/email-campaigns</c>, the paged list that
/// searches the subject and filters by status.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListEmailCampaignsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The campaigns one seeded mix holds, by the role each plays in the tests.</summary>
    /// <param name="Token">The text every seeded subject carries, so a search scopes a request to this mix.</param>
    /// <param name="Draft">A draft campaign, the oldest.</param>
    /// <param name="Scheduled">A scheduled campaign.</param>
    /// <param name="Sent">A sent campaign, the newest.</param>
    private sealed record CampaignMix(string Token, int Draft, int Scheduled, int Sent);

    /// <summary>The list holds the campaigns of a page, the newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsTheCampaigns_NewestFirst()
    {
        var mix = await SeedMixAsync();

        var (ids, totalCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}");

        ids.Should().Equal(mix.Sent, mix.Scheduled, mix.Draft);
        totalCount.Should().Be(3);
    }

    /// <summary>A row carries the campaign without its body and segments, and the stats of its own recipients only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RowCarriesItsOwnStats()
    {
        using var host = new ClockedHost(factory);
        var client = await AuthTestSeeds.AdminClientAsync(factory, host);
        var token = Guid.NewGuid().ToString("N")[..10];
        var draft = await CampaignTestSeeds.CreateAsync(client, subject: $"{token} draft row");
        var sending = await CampaignTestSeeds.CreateAsync(client, subject: $"{token} sending row");
        await CampaignTestSeeds.ScheduleAsync(client, sending, host.Clock.GetUtcNow().AddDays(1));
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, sending);

        var rows = (await PageAsync($"page=1&pageSize=100&search={token}")).GetProperty("items");

        var draftRow = rows.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == draft);
        draftRow.GetProperty("status").GetString().Should().Be("Draft");
        draftRow.GetProperty("stats").GetProperty("total").GetInt32().Should().Be(0);
        draftRow.TryGetProperty("body", out _).Should().BeFalse();
        draftRow.TryGetProperty("segments", out _).Should().BeFalse();
        var sendingRow = rows.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == sending);
        sendingRow.GetProperty("status").GetString().Should().Be("Sending");
        sendingRow.GetProperty("scheduledAt").GetDateTimeOffset().Should().BeAfter(host.Clock.GetUtcNow());
        var stats = sendingRow.GetProperty("stats");
        stats.GetProperty("total").GetInt32().Should().Be(4);
        stats.GetProperty("pending").GetInt32().Should().Be(1);
        stats.GetProperty("sent").GetInt32().Should().Be(1);
        stats.GetProperty("failed").GetInt32().Should().Be(1);
        stats.GetProperty("unsubscribed").GetInt32().Should().Be(1);
    }

    /// <summary>The search matches the subject in any letter case, as a substring, and leaves other campaigns out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchMatchesTheSubjectCaseInsensitively()
    {
        var mix = await SeedMixAsync();

        var (ids, totalCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token.ToUpperInvariant()}%20SCHEDULED");

        ids.Should().Equal(mix.Scheduled);
        totalCount.Should().Be(1);
    }

    /// <summary>A <c>%</c> or <c>_</c> typed in the search matches itself, not any text or any single character.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchWildcardsMatchThemselves()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var literal = await CampaignTestSeeds.CreateAsync(client, subject: $"{token} 50% off_sale");
        await CampaignTestSeeds.CreateAsync(client, subject: $"{token} 50 percent offXsale");

        var (percent, percentCount) = await ListAsync($"page=1&pageSize=100&search={token}%2050%25%20off_");
        var (anyText, anyTextCount) = await ListAsync($"page=1&pageSize=100&search={token}%25sale");
        var (anyChar, anyCharCount) = await ListAsync($"page=1&pageSize=100&search={token}%20offX_ale");

        percent.Should().Equal(literal);
        percentCount.Should().Be(1);
        anyText.Should().BeEmpty();
        anyTextCount.Should().Be(0);
        anyChar.Should().BeEmpty();
        anyCharCount.Should().Be(0);
    }

    /// <summary>The status filter keeps only the campaigns in that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_StatusFilterKeepsOnlyThatStatus()
    {
        var mix = await SeedMixAsync();

        var (draft, draftCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&status=Draft");
        var (scheduled, _) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&status=Scheduled");
        var (sending, sendingCount) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&status=Sending");
        var (sent, _) = await ListAsync($"page=1&pageSize=100&search={mix.Token}&status=Sent");

        draft.Should().Equal(mix.Draft);
        draftCount.Should().Be(1);
        scheduled.Should().Equal(mix.Scheduled);
        sending.Should().BeEmpty();
        sendingCount.Should().Be(0);
        sent.Should().Equal(mix.Sent);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IsPagedAfterTheFilters()
    {
        var mix = await SeedMixAsync();

        var (first, firstCount) = await ListAsync($"page=1&pageSize=2&search={mix.Token}");
        var (second, secondCount) = await ListAsync($"page=2&pageSize=2&search={mix.Token}");
        var (beyond, _) = await ListAsync($"page=3&pageSize=2&search={mix.Token}");

        first.Should().Equal(mix.Sent, mix.Scheduled);
        second.Should().Equal(mix.Draft);
        firstCount.Should().Be(3);
        secondCount.Should().Be(3);
        beyond.Should().BeEmpty();
    }

    /// <summary>A page, a page size or a search outside its limits is refused with 400 on that field rather than cut or defaulted.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=1&pageSize=101", "pageSize")]
    public async Task List_PagingOutsideTheLimits_IsRefused(string queryString, string field)
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"{CampaignTestSeeds.Route}?{queryString}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchOverTheLimit_IsRefused()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync(
            $"{CampaignTestSeeds.Route}?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "search");
    }

    /// <summary>A status that names nothing, by name or by number, is refused with 400 rather than listing every campaign.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("status=Nonsense")]
    [InlineData("status=99")]
    public async Task List_UnknownStatus_IsRefused(string filter)
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"{CampaignTestSeeds.Route}?page=1&pageSize=20&{filter}");

        // The model binder refuses a value that is no member before the validator runs, so the check is that the field is named.
        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "status");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"{CampaignTestSeeds.Route}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Seeds a draft, a scheduled and a sent campaign, created in that order so the newest is the sent one.</summary>
    /// <returns>The mix.</returns>
    private async Task<CampaignMix> SeedMixAsync()
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var draft = await CampaignTestSeeds.CreateAsync(client, subject: $"{token} draft");
        var scheduled = await CampaignTestSeeds.CreateAsync(client, subject: $"{token} scheduled");
        await CampaignTestSeeds.ScheduleAsync(client, scheduled);
        var sent = await CampaignTestSeeds.CreateAsync(client, subject: $"{token} sent");
        await CampaignTestSeeds.ScheduleAsync(client, sent);
        await CampaignTestSeeds.SeedSendingWithRecipientsAsync(factory, sent);
        await CampaignTestSeeds.SeedSentAsync(factory, sent);
        return new CampaignMix(token, draft, scheduled, sent);
    }

    /// <summary>Reads the page the admin list answers for a query string.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <returns>The page body.</returns>
    private async Task<JsonElement> PageAsync(string queryString)
    {
        var client = await ClientsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"{CampaignTestSeeds.Route}?{queryString}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }

    /// <summary>Lists the campaigns the admin list answers for a query string.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <returns>The ids of the page in the order the list gave them, and the total count of matches.</returns>
    private async Task<(List<int> Ids, int TotalCount)> ListAsync(string queryString)
    {
        var page = await PageAsync(queryString);
        var ids = page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        return (ids, page.GetProperty("totalCount").GetInt32());
    }
}
