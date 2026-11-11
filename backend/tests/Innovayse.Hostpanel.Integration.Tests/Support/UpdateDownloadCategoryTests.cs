using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/downloads/categories/{id}</c>: an admin can update a download category, an id
/// names no category is refused <c>DOWNLOAD_CATEGORY_NOT_FOUND</c>, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateDownloadCategoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update an existing download category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateADownloadCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateDownloadCategoryAsync(client);

        var response = await client.PutAsJsonAsync($"/api/downloads/categories/{id}", new
        {
            name = "Renamed",
            description = "Updated.",
            isHidden = false,
            parentCategoryId = (int?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Updating a download category that does not exist answers 404 with
    /// <c>DOWNLOAD_CATEGORY_NOT_FOUND</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownDownloadCategoryAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/downloads/categories/{SupportTestSeeds.NoSuchId}", new
        {
            name = "Anything",
            description = "Anything.",
            isHidden = false,
            parentCategoryId = (int?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DOWNLOAD_CATEGORY_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update a download category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/downloads/categories/{SupportTestSeeds.NoSuchId}", new
            {
                name = "Anything",
                description = "Anything.",
                isHidden = false,
                parentCategoryId = (int?)null,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
