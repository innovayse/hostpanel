using System.Net;
using System.Text;
using Innovayse.Hostpanel.API.Controllers;
using Microsoft.AspNetCore.TestHost;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// The rate limit of the public unsubscribe routes over HTTP: the real <see cref="PublicUnsubscribeController"/> behind the real
/// limiter (see <see cref="StrictRateLimitedHost"/>), with the strict budget cut to a few requests. The page endpoint is held
/// to it; the one-click action, which mail providers post from shared addresses, is not. The bus of that host has no handlers,
/// so what the limiter did is read from the one status it alone produces, 429, and not from whatever the action then answers.
/// </summary>
public sealed class PublicUnsubscribeRateLimitTests
{
    /// <summary>The route of the page endpoint.</summary>
    private const string Route = "/api/public/unsubscribe";

    /// <summary>The route of the one-click action.</summary>
    private const string OneClickRoute = "/api/public/unsubscribe/one-click?token=abc";

    /// <summary>The requests the strict tier allows in this test.</summary>
    private const int Permits = 2;

    /// <summary>A body the action binds; its token does not matter, because the bus has no handler.</summary>
    private const string Body = "{\"token\":\"abc\"}";

    /// <summary>Sends the page request.</summary>
    /// <param name="http">The client of the host.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PostAsync(HttpClient http)
    {
        return http.PostAsync(Route, new StringContent(Body, Encoding.UTF8, "application/json"));
    }

    /// <summary>Once the strict budget of the address is spent, the next page request is refused with 429 and a retry hint.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsubscribe_BeyondTheStrictBudget_RefusesTooManyRequests()
    {
        await using var app = await StrictRateLimitedHost.StartAsync<PublicUnsubscribeController>(Permits);
        using var http = app.GetTestClient();

        for (var i = 0; i < Permits; i++)
        {
            using var allowed = await PostAsync(http);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using var refused = await PostAsync(http);

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.Should().NotBeNull();
    }

    /// <summary>The one-click action is not held to the strict tier: mail providers post for all their users from a few addresses.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneClick_BeyondTheStrictBudget_IsNotRefused()
    {
        await using var app = await StrictRateLimitedHost.StartAsync<PublicUnsubscribeController>(Permits);
        using var http = app.GetTestClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < Permits * 3; i++)
        {
            using var response = await http.PostAsync(OneClickRoute, new StringContent("List-Unsubscribe=One-Click", Encoding.UTF8, "application/x-www-form-urlencoded"));
            statuses.Add(response.StatusCode);
        }

        statuses.Should().HaveCount(Permits * 3).And.AllSatisfy(status => status.Should().NotBe(HttpStatusCode.TooManyRequests));
    }
}
