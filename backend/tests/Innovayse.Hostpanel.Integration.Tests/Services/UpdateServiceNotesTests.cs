using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceNotes;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>PUT /api/services/{id}/notes</c> and <see cref="UpdateServiceNotesHandler"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateServiceNotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A valid save answers 204 and stores the notes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNotes_OnAnExistingService_StoresTheNotesAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/notes", new { adminNotes = "Call before renewing." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.AdminNotes.Should().Be("Call before renewing.");
    }

    /// <summary>Notes over the stored length are refused with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNotes_TooLong_Returns400ValidationErrorAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/notes", new { adminNotes = new string('n', 2001) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Saving the notes of a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNotes_OnAnUnknownService_Returns404Async()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/notes", new { adminNotes = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Saving without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNotes_WithoutAuth_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/notes", new { adminNotes = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
