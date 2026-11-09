using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/me/services/{id}/cancellation-status</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyServiceCancellationStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reading a stranger's cancellation status is refused rather than answered as "not pending".</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancellationStatus_AStrangersService_Returns404WithServiceNotFound()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "cancel-status-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.GetAsync($"/api/me/services/{serviceId}/cancellation-status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>The owner reads their own service's cancellation status without a stray refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancellationStatus_TheCallersOwnService_Returns200WithNoOpenRequest()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "cancel-status-success");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);

        var response = await owner.GetAsync($"/api/me/services/{serviceId}/cancellation-status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pending").GetBoolean().Should().BeFalse();
    }

    /// <summary>Reading cancellation status without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancellationStatus_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/services/{ServicesTestSeeds.NoSuchServiceId}/cancellation-status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
