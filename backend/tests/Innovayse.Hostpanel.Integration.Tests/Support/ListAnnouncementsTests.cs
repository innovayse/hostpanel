using System.Net;
using Innovayse.Hostpanel.Application.Common;
using static Innovayse.Hostpanel.Integration.Tests.Fixtures.PagedListReads;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/announcements</c>: an admin lists every announcement, drafts included, searched over the title and the
/// content, narrowed by the published state and paged on the server, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListAnnouncementsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The search matches the title and the content in any letter case, and nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheTitleAndTheContentInAnyCase()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var byTitle = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"Maintenance {token}");
        var byContent = await SupportListTestSeeds.SeedAnnouncementAsync(factory, "Notice", $"Window {token}");
        await SupportListTestSeeds.SeedAnnouncementAsync(factory, "Unrelated", "Nothing here.");

        var page = await PageAsync(admin, $"/api/announcements?page=1&pageSize=100&search={token.ToUpperInvariant()}");

        IdsOf(page).Should().BeEquivalentTo([byTitle, byContent]);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var percent = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"{token} 50% off");
        await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"{token} 50x off");
        var underscore = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"{token} a_c");
        await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"{token} abc");

        var percentPage = await PageAsync(admin, $"/api/announcements?page=1&pageSize=100&search={token}%2050%25");
        var underscorePage = await PageAsync(admin, $"/api/announcements?page=1&pageSize=100&search={token}%20a_c");
        var bareWildcard = await PageAsync(admin, $"/api/announcements?page=1&pageSize=100&search={token}%25%25");

        IdsOf(percentPage).Should().Equal(percent);
        IdsOf(underscorePage).Should().Equal(underscore);
        IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>The published filter keeps the published announcements or the drafts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsPublishedFilterKeepsPublishedOrDrafts()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var published = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"Live {token}", isPublished: true);
        var draft = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"Draft {token}", isPublished: false);

        var publishedPage = await PageAsync(admin, $"/api/announcements?page=1&pageSize=100&search={token}&isPublished=true");
        var draftPage = await PageAsync(admin, $"/api/announcements?page=1&pageSize=100&search={token}&isPublished=false");
        var both = await PageAsync(admin, $"/api/announcements?page=1&pageSize=100&search={token}");

        IdsOf(publishedPage).Should().Equal(published);
        IdsOf(draftPage).Should().Equal(draft);
        IdsOf(both).Should().BeEquivalentTo([published, draft]);
    }

    /// <summary>A filter is applied before paging: the total counts every match, each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var oldest = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"One {token}");
        var middle = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"Two {token}");
        var newest = await SupportListTestSeeds.SeedAnnouncementAsync(factory, $"Three {token}");
        await SupportListTestSeeds.SeedAnnouncementAsync(factory, "Not a match");
        var route = $"/api/announcements?pageSize=2&search={token}";

        var first = await PageAsync(admin, $"{route}&page=1");
        var second = await PageAsync(admin, $"{route}&page=2");
        var beyond = await PageAsync(admin, $"{route}&page=3");

        IdsOf(first).Should().Equal(newest, middle);
        first.GetProperty("totalCount").GetInt32().Should().Be(3);
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
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/announcements?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/announcements?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain("search");
    }

    /// <summary>A published state that is not a boolean is refused with 400 by the model binder.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnparsablePublishedStateIsRefused()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/announcements?page=1&pageSize=20&isPublished=maybe");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with no credential cannot list announcements.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/announcements?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
