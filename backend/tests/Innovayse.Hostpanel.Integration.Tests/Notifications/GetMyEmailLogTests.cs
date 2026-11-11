using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Notifications.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>GetMyEmailLog</c> use case: <c>GET /api/me/emails/{id}</c> resolves
/// the caller inside the handler and reads one email only when it was sent to them.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyEmailLogTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The caller reads one of their own emails, body included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetReturnsTheCallersEmail()
    {
        var (http, _, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        var id = await NotificationsTestSeeds.SeedLogAsync(factory, email, "Mine, read");

        var response = await http.GetAsync($"/api/me/emails/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        entry.GetProperty("subject").GetString().Should().Be("Mine, read");
    }

    /// <summary>A failed send is read as failed, with no failure reason, which is for staff only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetOmitsTheDeliveryError()
    {
        var (http, _, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        var id = await NotificationsTestSeeds.SeedLogAsync(factory, email, "Mine, failed", EmailSendFailure.AuthenticationRejected);

        var response = await http.GetAsync($"/api/me/emails/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(nameof(EmailSendFailure.AuthenticationRejected));
        var entry = System.Text.Json.JsonDocument.Parse(body).RootElement;
        entry.GetProperty("success").GetBoolean().Should().BeFalse();
        entry.TryGetProperty("error", out _).Should().BeFalse();
        entry.TryGetProperty("failure", out _).Should().BeFalse();
    }

    /// <summary>An email addressed to somebody else answers 404 with <c>EMAIL_LOG_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesSomebodyElsesEmail()
    {
        var (http, _, _, _) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        var othersId = await NotificationsTestSeeds.SeedLogAsync(
            factory, $"other-{Guid.NewGuid():N}@example.test", "Theirs, read");

        var response = await http.GetAsync($"/api/me/emails/{othersId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await NotificationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_LOG_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/emails/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
