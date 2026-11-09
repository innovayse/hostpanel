using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ChangeServiceStatus;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>PUT /api/services/{id}/status</c> and <see cref="ChangeServiceStatusHandler"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ChangeServiceStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Moving a suspended service to Active answers 204 and lifts the suspension.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeStatus_FromSuspendedToActive_UnsuspendsAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Suspended);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/status", new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.Status.Should().Be(ServiceStatus.Active);
        stored.SuspendedAt.Should().BeNull();
    }

    /// <summary>
    /// Asking to move a service back to Pending is refused with <c>SERVICE_STATUS_CHANGE_NOT_ALLOWED</c>
    /// and leaves the service untouched.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeStatus_ToPending_Returns400StatusChangeNotAllowedAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/status", new { status = "Pending" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_STATUS_CHANGE_NOT_ALLOWED);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>
    /// Moves the status table refuses answer 400 <c>SERVICE_STATUS_CHANGE_NOT_ALLOWED</c> before any
    /// transition runs, and leave the service as it was: nothing leaves Terminated, and a Pending
    /// service cannot be suspended or unsuspended.
    /// </summary>
    /// <param name="from">The status the service is in.</param>
    /// <param name="target">The status asked for.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ServiceStatus.Terminated, "Active")]
    [InlineData(ServiceStatus.Terminated, "Suspended")]
    [InlineData(ServiceStatus.Pending, "Suspended")]
    [InlineData(ServiceStatus.Pending, "Active")]
    public async Task ChangeStatus_OutsideTheTable_Returns400StatusChangeNotAllowedAsync(ServiceStatus from, string target)
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, from);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/status", new { status = target });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_STATUS_CHANGE_NOT_ALLOWED);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.Status.Should().Be(from);
    }

    /// <summary>
    /// Moving a pending service to Terminated runs the termination use case, and asking for the
    /// current status is a no-op that answers 204.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeStatus_PendingToTerminated_AndToTheSameStatus_Return204Async()
    {
        var pendingId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var activeId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var terminate = await client.PutAsJsonAsync($"/api/services/{pendingId}/status", new { status = "Terminated" });
        var same = await client.PutAsJsonAsync($"/api/services/{activeId}/status", new { status = "Active" });

        terminate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        same.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == pendingId)).Status.Should().Be(ServiceStatus.Terminated);
        (await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == activeId)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>
    /// A status this deployment does not know never reaches the handler: the body does not bind to
    /// <see cref="ServiceStatus"/>, and the refusal is the plain 400 <c>VALIDATION_ERROR</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeStatus_WithAnUnknownStatus_Returns400ValidationErrorAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/status", new { status = "NoSuchStatus" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Changing the status of a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeStatus_OnAnUnknownService_Returns404Async()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/status", new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Changing the status without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeStatus_WithoutAuth_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/status", new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
