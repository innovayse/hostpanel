using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>DELETE /api/predefined-replies/categories/{id}</c>: an admin can delete a predefined reply
/// category, an id names no category is refused <c>PREDEFINED_REPLY_CATEGORY_NOT_FOUND</c>, and
/// the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeletePredefinedReplyCategoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create and delete a predefined reply category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanDeleteACategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreatePredefinedReplyCategoryAsync(client);

        var response = await client.DeleteAsync($"/api/predefined-replies/categories/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Deleting a reply category that does not exist answers 404 with
    /// <c>PREDEFINED_REPLY_CATEGORY_NOT_FOUND</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteUnknownReplyCategoryAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/predefined-replies/categories/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PREDEFINED_REPLY_CATEGORY_NOT_FOUND);
    }

    /// <summary>
    /// A predefined reply category with a sub-category is refused with 409
    /// <c>PREDEFINED_REPLY_CATEGORY_IN_USE</c> and kept; once the sub-category is gone it can be deleted.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesACategoryWithSubCategoriesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var parentId = await SupportTestSeeds.CreatePredefinedReplyCategoryAsync(client);
        var child = await client.PostAsJsonAsync("/api/predefined-replies/categories", new
        {
            name = $"Child-{Guid.NewGuid():N}",
            parentCategoryId = parentId,
        });
        child.StatusCode.Should().Be(HttpStatusCode.Created);
        var childId = int.Parse((await child.Content.ReadAsStringAsync()).Trim());

        var refused = await client.DeleteAsync($"/api/predefined-replies/categories/{parentId}");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await SupportTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.PREDEFINED_REPLY_CATEGORY_IN_USE);
        (await client.DeleteAsync($"/api/predefined-replies/categories/{childId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/predefined-replies/categories/{parentId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A predefined reply category that still holds a reply is refused with 409
    /// <c>PREDEFINED_REPLY_CATEGORY_IN_USE</c>; once the reply is gone it can be deleted.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesACategoryWithRepliesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreatePredefinedReplyCategoryAsync(client);
        var reply = await client.PostAsJsonAsync("/api/predefined-replies", new
        {
            name = $"Reply-{Guid.NewGuid():N}",
            content = "Thanks for reaching out.",
            categoryId,
        });
        reply.StatusCode.Should().Be(HttpStatusCode.Created);
        var replyId = int.Parse((await reply.Content.ReadAsStringAsync()).Trim());

        var refused = await client.DeleteAsync($"/api/predefined-replies/categories/{categoryId}");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await SupportTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.PREDEFINED_REPLY_CATEGORY_IN_USE);
        (await client.DeleteAsync($"/api/predefined-replies/{replyId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/predefined-replies/categories/{categoryId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A caller with no credential cannot delete a predefined reply category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(
            $"/api/predefined-replies/categories/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
