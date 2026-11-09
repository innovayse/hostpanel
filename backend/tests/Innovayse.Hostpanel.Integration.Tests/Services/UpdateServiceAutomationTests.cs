using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceAutomation;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>PUT /api/services/{id}/automation</c> and <see cref="UpdateServiceAutomationHandler"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateServiceAutomationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A valid save answers 204 and stores the hold and the end-of-cycle termination.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateAutomation_WithHoldAndTermination_StoresBothAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var holdUntil = new DateTimeOffset(2031, 5, 6, 0, 0, 0, TimeSpan.Zero);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/automation", new
        {
            overrideAutoSuspend = true,
            suspendUntil = holdUntil,
            autoTerminateEndOfCycle = true,
            autoTerminateReason = "Moving away",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.OverrideAutoSuspend.Should().BeTrue();
        stored.SuspendUntil.Should().Be(holdUntil);
        stored.AutoTerminateEndOfCycle.Should().BeTrue();
        stored.AutoTerminateReason.Should().Be("Moving away");
    }

    /// <summary>
    /// Switching a service to manual billing answers 204 and stores <c>AutoRenew</c> off; a body without
    /// the field keeps automatic billing.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateAutomation_ManualBilling_StoresAutoRenewOffAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var manual = await client.PutAsJsonAsync($"/api/services/{serviceId}/automation", new
        {
            autoRenew = false,
            overrideAutoSuspend = false,
            autoTerminateEndOfCycle = false,
        });

        manual.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AutoRenewOfAsync(serviceId)).Should().BeFalse();

        var withoutField = await client.PutAsJsonAsync($"/api/services/{serviceId}/automation", new
        {
            overrideAutoSuspend = false,
            autoTerminateEndOfCycle = false,
        });

        withoutField.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AutoRenewOfAsync(serviceId)).Should().BeTrue();
    }

    /// <summary>Reads the stored <c>AutoRenew</c> flag of a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The flag as stored.</returns>
    private async Task<bool> AutoRenewOfAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => s.AutoRenew)
            .SingleAsync();
    }

    /// <summary>A hold date without the hold is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateAutomation_WithAHoldDateButNoHold_Returns400ValidationErrorAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/automation", new
        {
            overrideAutoSuspend = false,
            suspendUntil = new DateTimeOffset(2031, 5, 6, 0, 0, 0, TimeSpan.Zero),
            autoTerminateEndOfCycle = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Saving the settings of a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateAutomation_OnAnUnknownService_Returns404Async()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/automation",
            new { overrideAutoSuspend = false, autoTerminateEndOfCycle = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Saving without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateAutomation_WithoutAuth_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/automation",
            new { overrideAutoSuspend = false, autoTerminateEndOfCycle = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
