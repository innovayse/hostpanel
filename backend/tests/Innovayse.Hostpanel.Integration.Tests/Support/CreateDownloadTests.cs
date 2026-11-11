using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/downloads</c>: an admin can create a download entry, and the action is closed to
/// a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateDownloadTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create a download entry.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateADownloadAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreateDownloadCategoryAsync(client);

        var response = await client.PostAsJsonAsync("/api/downloads", new
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

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A category that does not exist is refused with 404 <c>DOWNLOAD_CATEGORY_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnUnknownCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/downloads", new
        {
            title = $"File-{Guid.NewGuid():N}",
            description = "A file.",
            type = "ZIP File",
            filename = "file.zip",
            categoryId = SupportTestSeeds.NoSuchId,
            clientsOnly = false,
            productDownload = false,
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DOWNLOAD_CATEGORY_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot create a download entry.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/downloads", new
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
