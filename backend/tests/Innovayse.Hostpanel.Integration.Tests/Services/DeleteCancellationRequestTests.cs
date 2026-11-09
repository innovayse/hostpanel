using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>DELETE /api/admin/cancellation-requests/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteCancellationRequestTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a cancellation request that does not exist answers 404, not a crash.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteCancellationRequest_OnAnUnknownId_Returns404()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/admin/cancellation-requests/{ServicesTestSeeds.NoSuchServiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CANCELLATION_REQUEST_NOT_FOUND);
    }

    /// <summary>Deleting an existing cancellation request succeeds with no content.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteCancellationRequest_OnAnExistingRequest_Returns204()
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "delete-cancel-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        (await owner.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.EndOfBillingPeriod), reason = (string?)null }))
            .EnsureSuccessStatusCode();

        int requestId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var request = context.CancellationRequests.First(r => r.ServiceId == serviceId);
            requestId = request.Id;
        }

        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var response = await admin.DeleteAsync($"/api/admin/cancellation-requests/{requestId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Deleting without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteCancellationRequest_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/admin/cancellation-requests/{ServicesTestSeeds.NoSuchServiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client calling the admin delete route is refused: the policy is admin only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteCancellationRequest_Returns403_AsPlainClientAsync()
    {
        var (client, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "delete-cancel-forbidden");

        var response = await client.DeleteAsync($"/api/admin/cancellation-requests/{ServicesTestSeeds.NoSuchServiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
