using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/tickets/{id}/reply</c>: staff can reply to a ticket, an id names no ticket is
/// refused <c>TICKET_NOT_FOUND</c>, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReplyToTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can reply to a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReplyToATicketAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{id}/reply", new { message = "We're on it." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>The reply is recorded under the signed-in staff member's name, taken from the token.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReplyAuthorIsTheSignedInStaffMemberAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync($"/api/tickets/{id}/reply", new { message = "We're on it." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LastReplyAuthorAsync(client, id)).Should().Be("Seeded Admin");
    }

    /// <summary>A name supplied in the body is ignored: identity comes from the token only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BodySuppliedAuthorNameIsIgnoredAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{id}/reply", new { message = "Spoofed.", authorName = "Somebody Else" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LastReplyAuthorAsync(client, id)).Should().Be("Seeded Admin");
    }

    /// <summary>Reads back the author name of the newest reply on a ticket.</summary>
    /// <param name="client">A client authenticated as Admin.</param>
    /// <param name="id">The ticket id.</param>
    /// <returns>The newest reply's author name.</returns>
    private static async Task<string?> LastReplyAuthorAsync(HttpClient client, int id)
    {
        using var body = JsonDocument.Parse(await client.GetStringAsync($"/api/tickets/{id}"));
        return body.RootElement.GetProperty("replies").EnumerateArray().Last().GetProperty("authorName").GetString();
    }

    /// <summary>Replying to a ticket that does not exist answers 404 with <c>TICKET_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReplyToUnknownTicketAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{SupportTestSeeds.NoSuchId}/reply", new { message = "Hello" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot reply to a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/tickets/{SupportTestSeeds.NoSuchId}/reply", new { message = "Hello" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
