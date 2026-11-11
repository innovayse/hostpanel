using System.Net;
using Innovayse.Hostpanel.API.Controllers;
using Microsoft.AspNetCore.TestHost;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// The rate limit of the open pixel and the tracked link over HTTP: they are held to the global backstop only, not to the strict
/// tier of the unsubscribe link. The real <see cref="PublicCampaignTrackingController"/> runs behind the real limiter (see
/// <see cref="StrictRateLimitedHost"/>), with the strict budget cut to a few requests, which these two routes must ignore. The
/// bus of that host has no handlers, so what the limiter did is read from the one status it alone produces, 429, and not from
/// whatever the action then answers.
/// </summary>
public sealed class PublicCampaignTrackingRateLimitTests
{
    /// <summary>The requests the strict tier allows in this test; the tests send three times as many.</summary>
    private const int Permits = 2;

    /// <summary>The token every request carries; its value does not matter, because the bus has no handler and a request inside the budget fails in the action.</summary>
    private static readonly string Token = new('a', 64);

    /// <summary>Sends more requests than the strict tier allows.</summary>
    /// <param name="route">The route to request.</param>
    /// <returns>The statuses of the requests, in order.</returns>
    private static async Task<IReadOnlyList<HttpStatusCode>> RequestPastTheStrictBudgetAsync(string route)
    {
        await using var app = await StrictRateLimitedHost.StartAsync<PublicCampaignTrackingController>(Permits);
        using var http = app.GetTestClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < Permits * 3; i++)
        {
            using var response = await http.GetAsync(route);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }

    /// <summary>The pixel is not held to the strict tier: image proxies of large mail providers fetch from few addresses, so only the global backstop binds it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Open_BeyondTheStrictBudget_IsNotRefused()
    {
        var statuses = await RequestPastTheStrictBudgetAsync($"/api/public/campaign-mail/open/{Token}");

        statuses.Should().HaveCount(Permits * 3).And.AllSatisfy(status => status.Should().NotBe(HttpStatusCode.TooManyRequests));
    }

    /// <summary>A tracked link is not held to the strict tier either, for the same reason.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Click_BeyondTheStrictBudget_IsNotRefused()
    {
        var statuses = await RequestPastTheStrictBudgetAsync($"/api/public/campaign-mail/click/{Token}/0");

        statuses.Should().HaveCount(Permits * 3).And.AllSatisfy(status => status.Should().NotBe(HttpStatusCode.TooManyRequests));
    }
}
