using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>DELETE /api/admin/knowledgebase/categories/{id}</c>: an admin can delete a knowledge base
/// category, an id names no category is refused <c>KBCATEGORY_NOT_FOUND</c>, and the action is
/// closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteKbCategoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create and delete a knowledge base category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanDeleteAKbCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateKbCategoryAsync(client);

        var response = await client.DeleteAsync($"/api/admin/knowledgebase/categories/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Deleting a knowledge base category that does not exist answers 404 with <c>KBCATEGORY_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteUnknownKbCategoryAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/admin/knowledgebase/categories/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
    }

    /// <summary>
    /// A knowledge base category with a sub-category is refused with 409 <c>KBCATEGORY_IN_USE</c> and
    /// kept; once the sub-category is gone it can be deleted.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesACategoryWithSubCategoriesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var parentId = await SupportTestSeeds.CreateKbCategoryAsync(client);
        var child = await client.PostAsJsonAsync("/api/admin/knowledgebase/categories", new
        {
            name = $"Child-{Guid.NewGuid():N}",
            description = "A sub-category.",
            isHidden = false,
            parentCategoryId = parentId,
        });
        child.StatusCode.Should().Be(HttpStatusCode.Created);
        var childId = int.Parse((await child.Content.ReadAsStringAsync()).Trim());

        var refused = await client.DeleteAsync($"/api/admin/knowledgebase/categories/{parentId}");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await SupportTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.KBCATEGORY_IN_USE);
        (await client.DeleteAsync($"/api/admin/knowledgebase/categories/{childId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/admin/knowledgebase/categories/{parentId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A caller with no credential cannot delete a knowledge base category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(
            $"/api/admin/knowledgebase/categories/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
