using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/tickets</c>: staff can file a ticket on behalf of a client, and the action is
/// closed to a caller with no credential.
/// </summary>
/// <remarks>
/// Admin-only, like every action of <see cref="Innovayse.Hostpanel.API.Controllers.TicketsController"/>.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AdminCreateTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can create a ticket on behalf of a client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateATicketAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(client);
        var clientId = (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId;

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            clientId,
            subject = "Admin-created ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A missing department is refused 400 on the <c>departmentId</c> field with a specific message.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingDepartmentIsRefusedOnTheDepartmentFieldAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            clientId = 1,
            subject = "Admin-created ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId = (int?)null,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors").GetProperty("departmentId")[0].GetString()
            .Should().Be("Select a department.");
    }

    /// <summary>A caller with no credential cannot file a ticket on behalf of a client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/tickets", new
        {
            clientId = 1,
            subject = "Anything",
            message = "Anything.",
            departmentId = SupportTestSeeds.NoSuchId,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A ticket for a client that does not exist is refused 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownClientIsRefusedNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(client);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            clientId = SupportTestSeeds.NoSuchId,
            subject = "Admin-created ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>A ticket in a department that does not exist is refused 404 with <c>DEPARTMENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownDepartmentIsRefusedNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var clientId = (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId;

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            clientId,
            subject = "Admin-created ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId = SupportTestSeeds.NoSuchId,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DEPARTMENT_NOT_FOUND);
    }
}
