using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Services.Queries.ListCancellationRequests;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/admin/cancellation-requests</c>: the page shape, the search on the reason and id, the
/// type and status filters applied before paging, the refusals and the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListCancellationRequestsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin reads the paged list of cancellation requests without a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAll_AsAdmin_Returns200()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/cancellation-requests?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The list answers the shared page shape: items with page number, size, total count and total pages.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAll_AsAdmin_AnswersTheSharedPageShapeAsync()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/cancellation-requests?page=2&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        root.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("pageNumber").GetInt32().Should().Be(2);
        root.GetProperty("pageSize").GetInt32().Should().Be(5);
        root.TryGetProperty("totalCount", out _).Should().BeTrue();
        root.TryGetProperty("totalPages", out _).Should().BeTrue();
        root.TryGetProperty("page", out _).Should().BeFalse();
    }

    /// <summary>Reading the list without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAll_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/cancellation-requests?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client calling the admin list route is refused: the policy is admin only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAll_Returns403_AsPlainClientAsync()
    {
        var (client, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "list-cancel-forbidden");

        var response = await client.GetAsync("/api/admin/cancellation-requests?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A request names the service's product even when the product has no price in the admin's currency, and the service's owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAll_NamesAProductWithNoPriceInTheCurrencyAsync()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "list-cancel-unpriced");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        (await owner.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.EndOfBillingPeriod), reason = (string?)null }))
            .EnsureSuccessStatusCode();
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/admin/cancellation-requests?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("serviceId").GetInt32() == serviceId);
        row.GetProperty("serviceName").GetString().Should().Be("Starter");
        row.GetProperty("clientName").GetString().Should().Be("Service Owner");
    }

    /// <summary>The search matches the reason case-insensitively as a substring and leaves other requests out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_FindsTheReasonInAnyLetterCaseAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await ServicesTestSeeds.SeedCancellationRequestsAsync(
            factory,
            serviceId,
            [
                (CancellationType.Immediate, $"{token} Moving abroad", false),
                (CancellationType.Immediate, $"{token} Too expensive", false),
                (CancellationType.Immediate, $"{token} Moving house", false),
            ]);
        var scope = "/api/admin/cancellation-requests?page=1&pageSize=100";

        var (moving, movingCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token.ToUpperInvariant()}%20MOVING");
        var (everything, everythingCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}");

        moving.Should().Equal(ids[2], ids[0]);
        movingCount.Should().Be(2);
        everything.Should().Equal(ids[2], ids[1], ids[0]);
        everythingCount.Should().Be(3);
    }

    /// <summary>A whole number typed into the search finds the request with that id, even when it has no reason.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_FindsTheRequestByIdAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var ids = await ServicesTestSeeds.SeedCancellationRequestsAsync(
            factory, serviceId, [(CancellationType.Immediate, null, false), (CancellationType.Immediate, null, false)]);

        var (found, count) = await PagedListReads.ReadIdsAsync(admin, $"/api/admin/cancellation-requests?page=1&pageSize=100&search={ids[0]}");

        // Other requests in the shared database may carry the digits in their reason; the reasonless sibling can only match by its own id.
        found.Should().Contain(ids[0]);
        found.Should().NotContain(ids[1]);
        count.Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_WildcardsAreLiteralAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await ServicesTestSeeds.SeedCancellationRequestsAsync(
            factory,
            serviceId,
            [
                (CancellationType.Immediate, $"{token}-50%", false),
                (CancellationType.Immediate, $"{token}-50X", false),
                (CancellationType.Immediate, $"{token}-A_C", false),
                (CancellationType.Immediate, $"{token}-ABC", false),
            ]);
        var scope = "/api/admin/cancellation-requests?page=1&pageSize=100";

        var (percent, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}-50%25");
        var (underscore, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}-A_C");
        var (bareWildcard, bareCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}%25%25");

        percent.Should().Equal(ids[0]);
        underscore.Should().Equal(ids[2]);
        bareWildcard.Should().BeEmpty();
        bareCount.Should().Be(0);
    }

    /// <summary>The type and the status filters, alone and together, keep only the requests that pass them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TypeAndStatusFiltersNarrowAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await ServicesTestSeeds.SeedCancellationRequestsAsync(
            factory,
            serviceId,
            [
                (CancellationType.Immediate, $"{token} a", false),
                (CancellationType.Immediate, $"{token} b", true),
                (CancellationType.EndOfBillingPeriod, $"{token} c", false),
                (CancellationType.EndOfBillingPeriod, $"{token} d", true),
            ]);
        var scope = $"/api/admin/cancellation-requests?page=1&pageSize=100&search={token}";

        var (immediate, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&type={nameof(CancellationType.Immediate)}");
        var (endOfPeriod, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&type={nameof(CancellationType.EndOfBillingPeriod)}");
        var (open, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&status={nameof(CancellationStatus.Open)}");
        var (closed, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&status={nameof(CancellationStatus.Closed)}");
        var (closedImmediate, closedImmediateCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&type=Immediate&status=Closed");

        immediate.Should().Equal(ids[1], ids[0]);
        endOfPeriod.Should().Equal(ids[3], ids[2]);
        open.Should().Equal(ids[2], ids[0]);
        closed.Should().Equal(ids[3], ids[1]);
        closedImmediate.Should().Equal(ids[1]);
        closedImmediateCount.Should().Be(1);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Filters_AreAppliedBeforePagingAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await ServicesTestSeeds.SeedCancellationRequestsAsync(
            factory,
            serviceId,
            [
                (CancellationType.Immediate, $"{token} a", false),
                (CancellationType.EndOfBillingPeriod, $"{token} b", false),
                (CancellationType.Immediate, $"{token} c", false),
                (CancellationType.Immediate, $"{token} d", false),
            ]);
        var scope = $"/api/admin/cancellation-requests?pageSize=2&search={token}&type=Immediate";

        var (first, firstCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=1");
        var (second, secondCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=2");
        var (beyond, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=3");

        first.Should().Equal(ids[3], ids[2]);
        firstCount.Should().Be(3);
        second.Should().Equal(ids[0]);
        secondCount.Should().Be(3);
        beyond.Should().BeEmpty();
    }

    /// <summary>A page below one or a page size outside the limit is refused with 400 naming the field, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "Page")]
    [InlineData("pageSize=20", "Page")]
    [InlineData("page=1&pageSize=0", "PageSize")]
    [InlineData("page=1&pageSize=101", "PageSize")]
    public async Task Paging_OutsideTheLimitsIsRefusedAsync(string queryString, string field)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/cancellation-requests?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, field)).Should().NotBeEmpty();
    }

    /// <summary>A search longer than the limit is refused with 400 naming the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_OverTheLimitIsRefusedAsync()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync(
            $"/api/admin/cancellation-requests?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, nameof(ListCancellationRequestsQuery.Search))).Should().NotBeEmpty();
    }

    /// <summary>A type or a status that names nothing, by name or by number, is refused with 400 naming the field, never read as no filter.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("type=NotAType", "Type")]
    [InlineData("type=99", "Type")]
    [InlineData("status=NotAStatus", "Status")]
    [InlineData("status=99", "Status")]
    public async Task UnknownFilterValue_IsRefusedAsync(string filter, string field)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/cancellation-requests?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, field)).Should().NotBeEmpty();
    }
}
