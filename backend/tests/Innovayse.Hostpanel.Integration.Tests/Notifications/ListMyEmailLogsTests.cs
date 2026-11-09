using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Notifications.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>ListMyEmailLogs</c> use case: <c>GET /api/me/emails</c> resolves the
/// caller inside the handler and lists only the mail addressed to them.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListMyEmailLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list holds the caller's own mail and never somebody else's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListReturnsOnlyTheCallersMail()
    {
        var (http, _, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        await NotificationsTestSeeds.SeedLogAsync(factory, email, "Mine, listed");
        await NotificationsTestSeeds.SeedLogAsync(factory, $"other-{Guid.NewGuid():N}@example.test", "Theirs, listed");

        var response = await http.GetAsync("/api/me/emails?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var subjects = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("subject").GetString()).ToList();
        subjects.Should().Contain("Mine, listed").And.NotContain("Theirs, listed");
    }

    /// <summary>A failed send is listed as failed, with no failure reason, which is for staff only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListOmitsTheDeliveryError()
    {
        var (http, _, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        await NotificationsTestSeeds.SeedLogAsync(factory, email, "Mine, failed", EmailSendFailure.AuthenticationRejected);

        var response = await http.GetAsync("/api/me/emails?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(nameof(EmailSendFailure.AuthenticationRejected));
        var page = System.Text.Json.JsonDocument.Parse(body).RootElement;
        var entry = page.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("subject").GetString() == "Mine, failed");
        entry.GetProperty("success").GetBoolean().Should().BeFalse();
        entry.TryGetProperty("error", out _).Should().BeFalse();
        entry.TryGetProperty("failure", out _).Should().BeFalse();
    }

    /// <summary>A page below 1 is refused by the validator with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesPageZero()
    {
        var (http, _, _, _) = await NotificationsTestSeeds.RegisterClientAsync(factory);

        var response = await http.GetAsync("/api/me/emails?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A page size above 100 is refused by the validator with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnOversizedPage()
    {
        var (http, _, _, _) = await NotificationsTestSeeds.RegisterClientAsync(factory);

        var response = await http.GetAsync("/api/me/emails?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A page size of 0 is refused by the validator with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAZeroPageSize()
    {
        var (http, _, _, _) = await NotificationsTestSeeds.RegisterClientAsync(factory);

        var response = await http.GetAsync("/api/me/emails?pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The list is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
