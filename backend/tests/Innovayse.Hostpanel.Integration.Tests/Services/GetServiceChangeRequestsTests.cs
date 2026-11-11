using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for the admin <c>GET /api/admin/service-change-requests</c> list: the pending queue, one service's plan change
/// history, and the client and product names a staff list shows.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServiceChangeRequestsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the list.</summary>
    private const string Route = "/api/admin/service-change-requests";

    /// <summary>Reads the ids of the requests on a page of the list.</summary>
    /// <param name="response">The list response.</param>
    /// <returns>The ids, in the order the page holds them.</returns>
    private static async Task<List<int>> IdsOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
    }

    /// <summary>The pending filter lists the requests waiting for staff and no closed one, with the names a list shows.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheStatusFilter_ListsThePendingQueueWithNames()
    {
        var waiting = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var done = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var waitingId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, waiting, ServiceChangeStatus.Pending);
        var doneId = await ServiceChangeTestSeeds.SeedRequestAsync(factory, done, ServiceChangeStatus.Completed);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?status=Pending&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await IdsOfAsync(response);
        ids.Should().Contain(waitingId).And.NotContain(doneId);

        var body = await (await admin.GetAsync($"{Route}?serviceId={waiting.ServiceId}")).Content.ReadFromJsonAsync<JsonElement>();
        var row = body.GetProperty("items").EnumerateArray().Single();
        row.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
        row.GetProperty("fromProductName").GetString().Should().Be("Starter");
        row.GetProperty("toProductName").GetString().Should().Be("Pro");
        row.GetProperty("type").GetString().Should().Be(nameof(ServiceChangeType.Downgrade));
        row.GetProperty("status").GetString().Should().Be(nameof(ServiceChangeStatus.Pending));
        row.GetProperty("currencyCode").GetString().Should().Be("USD");
        row.GetProperty("prorationAmount").GetDecimal().Should().Be(ServiceChangeTestSeeds.ExpectedProration(20m, 10m, waiting.NextRenewalAt));
    }

    /// <summary>One service's plan change history lists every request it ever had, newest first, and no other service's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheServiceFilter_ListsTheServicesHistoryNewestFirst()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var other = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        var first = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Rejected);
        var second = await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);
        var foreign = await ServiceChangeTestSeeds.SeedRequestAsync(factory, other, ServiceChangeStatus.Pending);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?serviceId={seeded.ServiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await IdsOfAsync(response);
        ids.Should().Equal(second, first);
        ids.Should().NotContain(foreign);
    }

    /// <summary>A page is cut to the page size and reports the total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APage_IsCutToThePageSize_AndReportsTheTotal()
    {
        var seeded = await ServiceChangeTestSeeds.SeedServiceAsync(factory, null, 20m, 10m);
        await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Rejected);
        await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Rejected);
        await ServiceChangeTestSeeds.SeedRequestAsync(factory, seeded, ServiceChangeStatus.Pending);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?serviceId={seeded.ServiceId}&page=1&pageSize=2");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
        body.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    /// <summary>A page number or size outside the limits, and a status that is not one, fail validation.</summary>
    /// <param name="query">The query string.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("serviceId=0")]
    [InlineData("status=Nonsense")]
    public async Task AnInvalidQuery_Returns400(string query)
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A client of the portal is not staff.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AClient_Returns403()
    {
        var client = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Without a session the route is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutASession_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
