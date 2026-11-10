using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/predefined-replies/categories</c>: an admin can create a predefined reply
/// category, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreatePredefinedReplyCategoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create a predefined reply category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateACategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/predefined-replies/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            parentCategoryId = (int?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A parent category that does not exist is refused with 404 <c>PREDEFINED_REPLY_CATEGORY_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnUnknownParentCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/predefined-replies/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            parentCategoryId = SupportTestSeeds.NoSuchId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PREDEFINED_REPLY_CATEGORY_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot create a predefined reply category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/predefined-replies/categories", new
        {
            name = "Anything",
            parentCategoryId = (int?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
