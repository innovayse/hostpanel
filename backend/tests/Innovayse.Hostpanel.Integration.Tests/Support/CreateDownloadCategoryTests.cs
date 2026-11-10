using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/downloads/categories</c>: an admin can create a download category, and the action
/// is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateDownloadCategoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create a download category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateADownloadCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/downloads/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            description = "A category.",
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A parent category that does not exist is refused with 404 <c>DOWNLOAD_CATEGORY_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnUnknownParentCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/downloads/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            description = "A category.",
            isHidden = false,
            parentCategoryId = SupportTestSeeds.NoSuchId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.DOWNLOAD_CATEGORY_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot create a download category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/downloads/categories", new
        {
            name = "Anything",
            description = "Anything.",
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
