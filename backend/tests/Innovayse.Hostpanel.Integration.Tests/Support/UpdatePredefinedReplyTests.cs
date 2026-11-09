using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/predefined-replies/{id}</c>: an admin can update a predefined reply, an id names
/// no reply is refused <c>PREDEFINED_REPLY_NOT_FOUND</c>,
/// and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdatePredefinedReplyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update an existing predefined reply.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateAReplyAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreatePredefinedReplyCategoryAsync(client);
        var createResponse = await client.PostAsJsonAsync("/api/predefined-replies", new
        {
            name = $"Reply-{Guid.NewGuid():N}",
            content = "Original content.",
            categoryId,
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await client.PutAsJsonAsync($"/api/predefined-replies/{id}", new
        {
            name = "Renamed",
            content = "Updated content.",
            categoryId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a reply that does not exist answers 404 with <c>PREDEFINED_REPLY_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownReplyAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreatePredefinedReplyCategoryAsync(client);

        var response = await client.PutAsJsonAsync($"/api/predefined-replies/{SupportTestSeeds.NoSuchId}", new
        {
            name = "Anything",
            content = "Anything.",
            categoryId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PREDEFINED_REPLY_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update a predefined reply.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/predefined-replies/{SupportTestSeeds.NoSuchId}", new
        {
            name = "Anything",
            content = "Anything.",
            categoryId = SupportTestSeeds.NoSuchId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
