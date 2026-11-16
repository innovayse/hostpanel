using System.Net;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using static Innovayse.Hostpanel.Integration.Tests.Fixtures.PagedListReads;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/network-issues</c>: an admin lists network issues, searched over the title, the server and the description,
/// narrowed by status, type and priority and paged on the server, and the list is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListNetworkIssuesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The search matches the title, the server and the description in any letter case, and nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTitleServerAndDescriptionInAnyCase()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var byTitle = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"Outage {token}");
        var byServer = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, "Outage", server: $"web-{token}");
        var byDescription = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, "Outage", description: $"Cause {token}");
        await SupportListTestSeeds.SeedNetworkIssueAsync(factory, "Unrelated", server: "db-01", description: "Nothing here.");

        var page = await PageAsync(admin, $"/api/network-issues?page=1&pageSize=100&search={token.ToUpperInvariant()}");

        IdsOf(page).Should().BeEquivalentTo([byTitle, byServer, byDescription]);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var percent = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"{token} 50% loss");
        await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"{token} 50x loss");
        var underscore = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"{token} a_c");
        await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"{token} abc");

        var percentPage = await PageAsync(admin, $"/api/network-issues?page=1&pageSize=100&search={token}%2050%25");
        var underscorePage = await PageAsync(admin, $"/api/network-issues?page=1&pageSize=100&search={token}%20a_c");
        var bareWildcard = await PageAsync(admin, $"/api/network-issues?page=1&pageSize=100&search={token}%25%25");

        IdsOf(percentPage).Should().Equal(percent);
        IdsOf(underscorePage).Should().Equal(underscore);
        IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>Each of the status, type and priority filters keeps only the issues that have it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusTypeAndPriorityFiltersKeepOnlyMatchingIssues()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var investigating = await SupportListTestSeeds.SeedNetworkIssueAsync(
            factory, $"A {token}", status: NetworkIssueStatus.Investigating, type: NetworkIssueType.Server, priority: NetworkIssuePriority.Low);
        var resolved = await SupportListTestSeeds.SeedNetworkIssueAsync(
            factory, $"B {token}", status: NetworkIssueStatus.Resolved, type: NetworkIssueType.Other, priority: NetworkIssuePriority.Critical);
        var route = $"/api/network-issues?page=1&pageSize=100&search={token}";

        var byStatus = await PageAsync(admin, $"{route}&status=Resolved");
        var byType = await PageAsync(admin, $"{route}&type=Server");
        var byPriority = await PageAsync(admin, $"{route}&priority=Critical");
        var combined = await PageAsync(admin, $"{route}&status=Investigating&type=Other");

        IdsOf(byStatus).Should().Equal(resolved);
        IdsOf(byType).Should().Equal(investigating);
        IdsOf(byPriority).Should().Equal(resolved);
        IdsOf(combined).Should().BeEmpty();
    }

    /// <summary>A filter is applied before paging: the total counts every match, each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var token = UniqueToken();
        var oldest = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"One {token}");
        var middle = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"Two {token}");
        var newest = await SupportListTestSeeds.SeedNetworkIssueAsync(factory, $"Three {token}");
        await SupportListTestSeeds.SeedNetworkIssueAsync(factory, "Not a match");
        var route = $"/api/network-issues?pageSize=2&search={token}";

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

        var response = await admin.GetAsync($"/api/network-issues?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/network-issues?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain("search");
    }

    /// <summary>
    /// A status, type or priority that names nothing, by name or by number, is refused with 400: the model binder turns it
    /// back before the validator runs, so the refusal is the validation code without a field of the validator's own.
    /// </summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("status=Archived")]
    [InlineData("status=99")]
    [InlineData("type=Sideways")]
    [InlineData("type=99")]
    [InlineData("priority=Urgent")]
    [InlineData("priority=99")]
    public async Task UnknownEnumFilterIsRefused(string filter)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/network-issues?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SupportTestSeeds.ReadCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>The network issue list is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/network-issues?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
