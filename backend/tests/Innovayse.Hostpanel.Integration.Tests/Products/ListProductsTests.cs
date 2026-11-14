using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/admin/products</c>: the paged staff product list. An admin reads one page in the operator's order, narrowed by
/// a search over the name, SKU and slug and by group, type and status; a value out of range or a filter that names nothing
/// is refused with 400 on its field; a caller with only the Client role is refused, and the route is closed to a caller
/// with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListProductsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/admin/products";

    /// <summary>The products one seeded mix holds, all in one fresh group so the group filter scopes a request to this mix.</summary>
    /// <param name="GroupId">The group every seeded product belongs to.</param>
    /// <param name="Second">An active shared hosting product at position 1; first in the order.</param>
    /// <param name="Third">A hidden VPS at position 2, named before <paramref name="Fourth"/>.</param>
    /// <param name="Fourth">An active VPS at position 2, named after <paramref name="Third"/>.</param>
    private sealed record ProductMix(int GroupId, int Second, int Third, int Fourth);

    /// <summary>A page lists the products by position, then name, and counts every match, the hidden ones included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsTheProducts_InTheOperatorsOrder()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync(admin);

        var (ids, totalCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={mix.GroupId}");

        ids.Should().Equal(mix.Second, mix.Third, mix.Fourth);
        totalCount.Should().Be(3);
    }

    /// <summary>A row is priced in the caller's currency by the mapper the picker read uses, and carries its status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RowIsMappedLikeThePickerRead()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync(admin);

        var page = await PagedListReads.PageAsync(admin, $"{Route}?page=1&pageSize=100&groupId={mix.GroupId}");
        var picker = await ProductsTestSeeds.GetProductAsync(admin, mix.Third);

        var row = page.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == mix.Third);
        row.GetRawText().Should().Be(picker.GetRawText());
        row.GetProperty("status").GetString().Should().Be("Inactive");
        row.GetProperty("pricing").GetProperty("monthly").GetDecimal().Should().Be(1m);
    }

    /// <summary>The search matches the name, the SKU and the slug in any letter case, as a substring.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchMatchesNameSkuAndSlug()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(admin);
        var token = PagedListReads.UniqueToken();
        var byName = await CreateAsync(admin, groupId, $"{token} Plan");
        var bySku = await CreateAsync(admin, groupId, "Plain two", sku: $"sku-{token[..12]}");
        var bySlug = await CreateAsync(admin, groupId, "Plain three", slug: $"slug-{token[..12]}");

        var (nameIds, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={groupId}&search={token.ToUpperInvariant()}");
        var (skuIds, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={groupId}&search=sku-{token[..12]}");
        var (slugIds, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={groupId}&search=SLUG-{token[..12]}");

        nameIds.Should().Equal(byName);
        skuIds.Should().Equal(bySku);
        slugIds.Should().Equal(bySlug);
    }

    /// <summary>A <c>%</c> or <c>_</c> typed in the search matches itself, not any text or any single character.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchWildcardsMatchThemselves()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(admin);
        var token = PagedListReads.UniqueToken();
        var literal = await CreateAsync(admin, groupId, $"{token} 50% off_sale");
        await CreateAsync(admin, groupId, $"{token} 50 percent offXsale");

        var (percent, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={groupId}&search={Uri.EscapeDataString($"{token} 50% off_")}");
        var (anyText, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={groupId}&search={Uri.EscapeDataString($"{token}%sale")}");
        var (anyChar, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={groupId}&search={Uri.EscapeDataString($"{token} 50 percent offX_ale")}");

        percent.Should().Equal(literal);
        anyText.Should().BeEmpty();
        anyChar.Should().BeEmpty();
    }

    /// <summary>The group filter keeps only the products of that group.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_GroupFilterKeepsOnlyThatGroup()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var first = await ProductsTestSeeds.CreateGroupAsync(admin);
        var second = await ProductsTestSeeds.CreateGroupAsync(admin);
        var inFirst = await CreateAsync(admin, first, "In the first group");
        var inSecond = await CreateAsync(admin, second, "In the second group");

        var (firstIds, firstCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={first}");
        var (secondIds, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={second}");

        firstIds.Should().Equal(inFirst);
        firstCount.Should().Be(1);
        secondIds.Should().Equal(inSecond);
    }

    /// <summary>The type and status filters keep only that type and that status, alone or together.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_TypeAndStatusFiltersKeepOnlyThose()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync(admin);

        var (vps, vpsCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={mix.GroupId}&type=Vps");
        var (shared, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={mix.GroupId}&type=sharedhosting");
        var (inactive, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={mix.GroupId}&status=Inactive");
        var (active, activeCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={mix.GroupId}&status=Active");
        var (both, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=100&groupId={mix.GroupId}&type=Vps&status=Active");

        vps.Should().Equal(mix.Third, mix.Fourth);
        vpsCount.Should().Be(2);
        shared.Should().Equal(mix.Second);
        inactive.Should().Equal(mix.Third);
        active.Should().Equal(mix.Second, mix.Fourth);
        activeCount.Should().Be(2);
        both.Should().Equal(mix.Fourth);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice in order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IsPagedAfterTheFilters()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var mix = await SeedMixAsync(admin);

        var (first, firstCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=1&pageSize=2&groupId={mix.GroupId}");
        var (second, secondCount) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=2&pageSize=2&groupId={mix.GroupId}");
        var (beyond, _) = await PagedListReads.ReadIdsAsync(admin, $"{Route}?page=3&pageSize=2&groupId={mix.GroupId}");

        first.Should().Equal(mix.Second, mix.Third);
        second.Should().Equal(mix.Fourth);
        firstCount.Should().Be(3);
        secondCount.Should().Be(3);
        beyond.Should().BeEmpty();
    }

    /// <summary>A page, a page size or a group out of range is refused with 400 on that field rather than cut or defaulted.</summary>
    /// <param name="queryString">The query string, without the leading question mark.</param>
    /// <param name="field">The field the refusal must name, in lower case.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    [InlineData("page=1&pageSize=20&groupId=0", "groupid")]
    [InlineData("page=1&pageSize=20&groupId=-3", "groupid")]
    public async Task List_ValueOutOfRange_IsRefused(string queryString, string field)
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchOverTheLimit_IsRefused()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain("search");
    }

    /// <summary>
    /// A type or a status that names nothing, by name or by number, is refused with 400 on its field rather than listing every
    /// product. The model binder refuses an unknown name before the validator runs and the validator refuses an unknown number.
    /// </summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name, in lower case.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("type=Nonsense", "type")]
    [InlineData("type=99", "type")]
    [InlineData("status=Nonsense", "status")]
    [InlineData("status=99", "status")]
    public async Task List_UnknownTypeOrStatus_IsRefused(string filter, string field)
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IsClosedToTheClientRole()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "list-products-forbidden");

        var response = await client.GetAsync($"{Route}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_IsClosedToAnonymousCallers()
    {
        var response = await factory.CreateClient().GetAsync($"{Route}?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Seeds, in one fresh group, an active shared hosting product at position 1, a hidden VPS named "B" and an active VPS
    /// named "C", both at position 2, created in an order that is neither the position nor the name order.
    /// </summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <returns>The mix.</returns>
    private static async Task<ProductMix> SeedMixAsync(HttpClient admin)
    {
        var groupId = await ProductsTestSeeds.CreateGroupAsync(admin);
        var fourth = await CreateAsync(admin, groupId, "C vps", ProductType.Vps, sortOrder: 2);
        var third = await CreateAsync(admin, groupId, "B vps", ProductType.Vps, sortOrder: 2, isHidden: true);
        var second = await CreateAsync(admin, groupId, "A shared", ProductType.SharedHosting, sortOrder: 1);
        return new ProductMix(groupId, second, third, fourth);
    }

    /// <summary>Creates a product through the API with a one dollar monthly price and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="groupId">The group the product belongs to.</param>
    /// <param name="name">The product name.</param>
    /// <param name="type">The product type.</param>
    /// <param name="sortOrder">The position inside the group.</param>
    /// <param name="isHidden">Whether the product is created hidden, which is inactive.</param>
    /// <param name="slug">The slug, or null for none.</param>
    /// <param name="sku">The SKU, or null for none.</param>
    /// <returns>The new product's id.</returns>
    private static async Task<int> CreateAsync(
        HttpClient admin,
        int groupId,
        string name,
        ProductType type = ProductType.SharedHosting,
        int sortOrder = 0,
        bool isHidden = false,
        string? slug = null,
        string? sku = null)
    {
        var response = await admin.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name,
            slug,
            sku,
            type,
            sortOrder,
            isHidden,
            monthlyPrice = 1m,
            annualPrice = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<int>();
    }
}
