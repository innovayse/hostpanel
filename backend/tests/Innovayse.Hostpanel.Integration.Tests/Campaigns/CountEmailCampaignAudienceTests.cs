using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Integration tests for the <c>CountEmailCampaignAudience</c> use case: <c>POST /api/admin/email-campaigns/audience-count</c>.
/// A product segment names a product the test created, so its count is exact whatever else the shared database holds.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CountEmailCampaignAudienceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the count.</summary>
    private const string Route = CampaignTestSeeds.Route + "/audience-count";

    /// <summary>Builds the body of a count request from segments.</summary>
    /// <param name="segments">The segments to count.</param>
    /// <returns>The request body.</returns>
    private static object Segments(params object[] segments) => new { segments };

    /// <summary>One segment of a filter and its value.</summary>
    /// <param name="filterType">The filter name.</param>
    /// <param name="filterValue">The filter's value.</param>
    /// <returns>The segment.</returns>
    private static object Segment(string filterType, string? filterValue) => new { filterType, filterValue };

    /// <summary>Counts the segments through the API and reads the number back.</summary>
    /// <param name="client">The signed-in admin client.</param>
    /// <param name="segments">The segments to count.</param>
    /// <returns>The count the API answered.</returns>
    private static async Task<int> CountAsync(HttpClient client, params object[] segments)
    {
        var response = await client.PostAsJsonAsync(Route, Segments(segments));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32();
    }

    /// <summary>Only clients who agreed to marketing email with a service of the product are counted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_ProductSegment_CountsOnlyOptedInOwners()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var optedIn = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var optedOut = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider, optedIn: false);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, optedIn.Client.Id);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, optedOut.Client.Id, productId);

        var count = await CountAsync(admin, Segment("Product", productId.ToString()));

        count.Should().Be(1);
    }

    /// <summary>A client matched by two segments is counted once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_ClientMatchedTwice_IsCountedOnce()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var owner = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var first = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, owner.Client.Id);
        var second = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, owner.Client.Id);

        var count = await CountAsync(admin, Segment("Product", first.ToString()), Segment("Product", second.ToString()));

        count.Should().Be(1);
    }

    /// <summary>A client whose account is not active is not counted, even when it owns the product and agreed to marketing email.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_NonActiveClient_IsNotCounted()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var active = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var suspended = await CampaignTestSeeds.SeedSuspendedAudienceClientAsync(scope.ServiceProvider);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, active.Client.Id);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, suspended.Client.Id, productId);

        var count = await CountAsync(admin, Segment("Product", productId.ToString()));

        count.Should().Be(1);
    }

    /// <summary>Clients whose accounts reach the same mailbox are counted once, as sending mails it once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_ClientsSharingAnAddress_CountTheAddressOnce()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var first = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var second = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        await CampaignTestSeeds.ShareAddressAsync(scope.ServiceProvider, second.Client, first.Email);
        var productId = await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, first.Client.Id);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, second.Client.Id, productId);

        var count = await CountAsync(admin, Segment("Product", productId.ToString()));

        count.Should().Be(1);
    }

    /// <summary>A service that renews within the days is counted; one that renews later is not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_ExpiringSegment_CountsOnlyServicesRenewingWithinTheDays()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var soon = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var later = await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);
        var before = await CountAsync(admin, Segment("ServiceExpiringInDays", "10"));

        // A monthly period that started 25 days ago renews in about five days; one that started now, in about thirty.
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, soon.Client.Id, periodStartedDaysAgo: 25);
        await CampaignTestSeeds.SeedServiceAsync(scope.ServiceProvider, later.Client.Id, periodStartedDaysAgo: 0);

        var after = await CountAsync(admin, Segment("ServiceExpiringInDays", "10"));

        after.Should().Be(before + 1);
    }

    /// <summary>A country segment counts the opted-in clients of that country, whatever case the code is sent in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_CountrySegment_CountsTheCountrysClients()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var before = await CountAsync(admin, Segment("Country", "IS"));
        await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider, country: "IS");
        await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider, optedIn: false, country: "IS");
        await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider, country: "NO");

        var after = await CountAsync(admin, Segment("Country", "is"));

        after.Should().Be(before + 1);
    }

    /// <summary>The all-active segment reaches every opted-in client, and no segments reach nobody.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_AllActiveAndNoSegments_ReachEveryoneAndNobody()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var before = await CountAsync(admin, Segment("AllActive", null));
        await CampaignTestSeeds.SeedAudienceClientAsync(scope.ServiceProvider);

        var after = await CountAsync(admin, Segment("AllActive", null));
        var none = await CountAsync(admin);

        after.Should().Be(before + 1);
        none.Should().Be(0);
    }

    /// <summary>A value that does not fit its filter is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_InvalidSegmentValue_RefusesValidation()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(Route, Segments(Segment("Country", "ARMENIA")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A missing segment list is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_WithoutTheSegmentList_RefusesValidation()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(Route, new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Count_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Route, Segments());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
