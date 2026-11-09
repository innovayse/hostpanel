using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/downloads/{id}</c>: an admin can update a download entry, an id names no download
/// is refused <c>DOWNLOAD_NOT_FOUND</c>, and the action is
/// closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateDownloadTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update an existing download entry.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateADownloadAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreateDownloadCategoryAsync(client);
        var createResponse = await client.PostAsJsonAsync("/api/downloads", new
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
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await client.PutAsJsonAsync($"/api/downloads/{id}", new
        {
            title = "Renamed",
            description = "Updated.",
            type = "PDF",
            filename = "file.pdf",
            categoryId,
            clientsOnly = true,
            productDownload = false,
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a download that does not exist answers 404 with <c>DOWNLOAD_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownDownloadAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreateDownloadCategoryAsync(client);

        var response = await client.PutAsJsonAsync($"/api/downloads/{SupportTestSeeds.NoSuchId}", new
        {
            title = "Anything",
            description = "Anything.",
            type = "ZIP File",
            filename = "file.zip",
            categoryId,
            clientsOnly = false,
            productDownload = false,
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DOWNLOAD_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update a download entry.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/downloads/{SupportTestSeeds.NoSuchId}", new
        {
            title = "Anything",
            description = "Anything.",
            type = "ZIP File",
            filename = "file.zip",
            categoryId = SupportTestSeeds.NoSuchId,
            clientsOnly = false,
            productDownload = false,
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
