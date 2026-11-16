using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Users;

/// <summary>
/// <c>GET /api/admin/users</c>: an admin reads a page of user summaries including one freshly
/// registered, the search narrows the list (a <c>%</c> or <c>_</c> in it matches itself), the page and page size
/// are required and refused outside their limits, a caller with only the Client role is refused, and the route
/// is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class ListUsersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A freshly registered account appears in the admin's page of user summaries.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterThenList_ShowsTheAccountAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, email) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.GetAsync($"/api/admin/users?page=1&pageSize=20&search={Uri.EscapeDataString(email)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().Contain(i => i.GetProperty("id").GetString() == subject);
    }

    /// <summary>The search keeps only the accounts that match it and the total counts them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchKeepsOnlyTheMatchingAccountsAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        var first = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient(), $"find-{token}");
        var second = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient(), $"find-{token}");
        var other = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient(), $"skip-{token}");

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/admin/users?page=1&pageSize=100&search=find-{token}");

        body.GetProperty("totalCount").GetInt32().Should().Be(2);
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString())
            .Should().BeEquivalentTo([first.UserId, second.UserId]).And.NotContain(other.UserId);
    }

    /// <summary>A <c>%</c> or <c>_</c> in the search is text to find, not a wildcard that matches every account.</summary>
    /// <param name="wildcard">The character typed after the token.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task SearchTreatsAWildcardAsTextAsync(string wildcard)
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        await UsersTestSeeds.RegisterUserAsync(factory.CreateClient(), $"wild-{token}");

        var body = await client.GetFromJsonAsync<JsonElement>(
            $"/api/admin/users?page=1&pageSize=100&search={Uri.EscapeDataString($"wild-{token}{wildcard}")}");

        body.GetProperty("totalCount").GetInt32().Should().Be(0);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    /// <summary>The total counts every match and each page holds its slice, without repeating a row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PagesTheSearchedListAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
        {
            await UsersTestSeeds.RegisterUserAsync(factory.CreateClient(), $"paged-{token}");
        }

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/admin/users?page=1&pageSize=2&search=paged-{token}");
        var second = await client.GetFromJsonAsync<JsonElement>($"/api/admin/users?page=2&pageSize=2&search=paged-{token}");

        first.GetProperty("totalCount").GetInt32().Should().Be(3);
        second.GetProperty("totalCount").GetInt32().Should().Be(3);
        var firstIds = first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToList();
        var secondIds = second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToList();
        firstIds.Should().HaveCount(2);
        secondIds.Should().HaveCount(1);
        firstIds.Should().NotIntersectWith(secondIds);
    }

    /// <summary>A missing page, a page below one or a page size outside the limit is refused with 400 on the field, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=1&pageSize=101", "pageSize")]
    public async Task PagingOutsideTheLimitsIsRefusedAsync(string queryString, string field)
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/users?{queryString}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefusedAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/users?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "search");
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await UsersTestSeeds.ClientRoleClientAsync(factory, "list-users-forbidden");

        var response = await client.GetAsync("/api/admin/users?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/users?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
