using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/me/tickets</c>: a client can create a ticket of their own, and the action is
/// closed to a caller with no credential.
/// </summary>
/// <remarks>
/// <c>CLIENT_PROFILE_NOT_FOUND</c> (the caller has no client record) is not reproduced here: every
/// caller with a Client-policy token this suite can mint already has one, because registration
/// creates it via Wolverine.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client can create a ticket of their own.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientCanCreateATicketAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(admin);
        var (owner, _) = await SupportTestSeeds.RegisterClientAsync(factory, "create-ticket-owner");

        var response = await owner.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Test Ticket",
            message = "This is a test ticket body.",
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
        var (owner, _) = await SupportTestSeeds.RegisterClientAsync(factory, "create-ticket-no-department");

        var response = await owner.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Test Ticket",
            message = "This is a test ticket body.",
            departmentId = (int?)null,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors").GetProperty("departmentId")[0].GetString()
            .Should().Be("Select a department.");
    }

    /// <summary>A caller with no credential cannot create a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Anything",
            message = "Anything.",
            departmentId = SupportTestSeeds.NoSuchId,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A ticket in a department that does not exist is refused 404 with <c>DEPARTMENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownDepartmentIsRefusedNotFoundAsync()
    {
        var (owner, _) = await SupportTestSeeds.RegisterClientAsync(factory, "create-ticket-unknown-department");

        var response = await owner.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Test Ticket",
            message = "This is a test ticket body.",
            departmentId = SupportTestSeeds.NoSuchId,
            priority = nameof(TicketPriority.Medium),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DEPARTMENT_NOT_FOUND);
    }
}
