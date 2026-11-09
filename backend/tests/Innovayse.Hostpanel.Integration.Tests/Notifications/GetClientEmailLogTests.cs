using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>GetClientEmailLog</c> use case: <c>GET /api/me/emails/{id}</c>,
/// the client portal's own reading of one email they were sent.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientEmailLogTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client reading one of their own emails gets the rendered body back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyEmails_Get_ReturnsTheEntryWithItsBody()
    {
        var (http, _, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        var id = await NotificationsTestSeeds.SeedLogAsync(factory, email, "Read me");

        var response = await http.GetAsync($"/api/me/emails/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        entry.GetProperty("subject").GetString().Should().Be("Read me");
        entry.GetProperty("body").GetString().Should().Be("<p>Body</p>");
    }

    /// <summary>An id addressed to somebody else answers 404 with <c>EMAIL_LOG_NOT_FOUND</c>, not somebody else's mail.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyEmails_Get_SomebodyElsesEntry_RefusesNotFound()
    {
        var (http, _, _, _) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        var othersId = await NotificationsTestSeeds.SeedLogAsync(
            factory, $"someone-else-{Guid.NewGuid():N}@example.test", "Not mine");

        var response = await http.GetAsync($"/api/me/emails/{othersId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await NotificationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_LOG_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyEmails_Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/emails/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
