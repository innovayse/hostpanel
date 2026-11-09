using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>DELETE /api/downloads/categories/{id}</c>: an admin can delete a download category, an id
/// names no category is refused <c>DOWNLOAD_CATEGORY_NOT_FOUND</c>, and the action is closed to a
/// caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteDownloadCategoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create and delete a download category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanDeleteADownloadCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateDownloadCategoryAsync(client);

        var response = await client.DeleteAsync($"/api/downloads/categories/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Deleting a download category that does not exist answers 404 with
    /// <c>DOWNLOAD_CATEGORY_NOT_FOUND</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteUnknownDownloadCategoryAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/downloads/categories/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DOWNLOAD_CATEGORY_NOT_FOUND);
    }

    /// <summary>
    /// A download category with a sub-category is refused with 409 <c>DOWNLOAD_CATEGORY_IN_USE</c> and
    /// kept; once the sub-category is gone it can be deleted.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesACategoryWithSubCategoriesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var parentId = await SupportTestSeeds.CreateDownloadCategoryAsync(client);
        var child = await client.PostAsJsonAsync("/api/downloads/categories", new
        {
            name = $"Child-{Guid.NewGuid():N}",
            description = "A sub-category.",
            isHidden = false,
            parentCategoryId = parentId,
        });
        child.StatusCode.Should().Be(HttpStatusCode.Created);
        var childId = int.Parse((await child.Content.ReadAsStringAsync()).Trim());

        var refused = await client.DeleteAsync($"/api/downloads/categories/{parentId}");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await SupportTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.DOWNLOAD_CATEGORY_IN_USE);
        (await client.DeleteAsync($"/api/downloads/categories/{childId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/downloads/categories/{parentId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A download category that still holds a download is refused with 409
    /// <c>DOWNLOAD_CATEGORY_IN_USE</c>; once the download is gone it can be deleted.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesACategoryWithDownloadsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreateDownloadCategoryAsync(client);
        var download = await client.PostAsJsonAsync("/api/downloads", new
        {
            title = $"File-{Guid.NewGuid():N}",
            description = "A file.",
            type = "ZIP File",
            filename = "file.zip",
            categoryId,
            clientsOnly = false,
            productDownload = false,
            isHidden = false,
        });
        download.StatusCode.Should().Be(HttpStatusCode.Created);
        var downloadId = int.Parse((await download.Content.ReadAsStringAsync()).Trim());

        var refused = await client.DeleteAsync($"/api/downloads/categories/{categoryId}");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await SupportTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.DOWNLOAD_CATEGORY_IN_USE);
        (await client.DeleteAsync($"/api/downloads/{downloadId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/downloads/categories/{categoryId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A caller with no credential cannot delete a download category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(
            $"/api/downloads/categories/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
