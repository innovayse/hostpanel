using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/tickets/{id}</c>: an admin can update a ticket's metadata, an id names no ticket
/// is refused <c>TICKET_NOT_FOUND</c>, and the action is
/// closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update a ticket's status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateATicketAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PutAsJsonAsync($"/api/tickets/{id}", new { status = nameof(TicketStatus.Closed) });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a ticket that does not exist answers 404 with <c>TICKET_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownTicketAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/tickets/{SupportTestSeeds.NoSuchId}", new { status = nameof(TicketStatus.Closed) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/tickets/{SupportTestSeeds.NoSuchId}", new { status = nameof(TicketStatus.Closed) });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A priority given by name is applied to the ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanChangeThePriorityByNameAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PutAsJsonAsync($"/api/tickets/{id}", new { priority = nameof(TicketPriority.High) });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var ticket = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == id);
        ticket.Priority.Should().Be(TicketPriority.High);
    }

    /// <summary>A priority that names no priority is refused with 400 and leaves the ticket alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownPriorityIsRejectedAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PutAsJsonAsync($"/api/tickets/{id}", new { priority = "Urgent" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A status that names no status is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownStatusIsRejectedAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PutAsJsonAsync($"/api/tickets/{id}", new { status = "Archived" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Moving a ticket to a department that does not exist is refused 404 with <c>DEPARTMENT_NOT_FOUND</c> and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownDepartmentIsRefusedNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PutAsJsonAsync(
            $"/api/tickets/{id}", new { departmentId = SupportTestSeeds.NoSuchId, priority = nameof(TicketPriority.High) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DEPARTMENT_NOT_FOUND);
    }
}
