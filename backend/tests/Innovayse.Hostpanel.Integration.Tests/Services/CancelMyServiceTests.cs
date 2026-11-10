using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>POST /api/me/services/{id}/cancel</c>: the owner may cancel their own
/// service, and a stranger is refused with the same <c>SERVICE_NOT_FOUND</c> a missing id would
/// answer.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CancelMyServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A stranger cancelling somebody else's service is refused, not told which part is wrong.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_AStrangersService_Returns404WithServiceNotFound()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "cancel-mine-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.Immediate), reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>The owner can cancel their own service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_TheCallersOwnService_Succeeds()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "cancel-mine-success");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);

        var response = await owner.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.Immediate), reason = "No longer needed" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A second open cancellation request on the caller's own service is refused as a conflict.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_ASecondOpenRequest_Returns409WithAlreadyPending()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "cancel-mine-conflict");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        (await owner.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.EndOfBillingPeriod), reason = "First" }))
            .EnsureSuccessStatusCode();

        var response = await owner.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.EndOfBillingPeriod), reason = "Second" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CANCELLATION_ALREADY_PENDING);
    }

    /// <summary>Cancelling without authentication is refused before ownership is even considered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/me/services/{ServicesTestSeeds.NoSuchServiceId}/cancel",
            new { type = nameof(CancellationType.Immediate), reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
