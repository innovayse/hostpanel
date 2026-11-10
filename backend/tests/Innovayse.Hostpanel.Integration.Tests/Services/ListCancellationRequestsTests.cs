using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/admin/cancellation-requests</c>.
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

        var response = await client.GetAsync("/api/admin/cancellation-requests");

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

        var response = await client.GetAsync("/api/admin/cancellation-requests");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client calling the admin list route is refused: the policy is admin only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAll_Returns403_AsPlainClientAsync()
    {
        var (client, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "list-cancel-forbidden");

        var response = await client.GetAsync("/api/admin/cancellation-requests");

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
}
