using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/admin/knowledgebase/categories</c>: an admin can create a knowledge base
/// category, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateKbCategoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create a knowledge base category.</summary>
    [Fact]
    public async Task AdminCanCreateAKbCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/knowledgebase/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            description = "A category.",
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A new category gets a slug from its name, and a second one with the same name gets a numbered slug.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreatedCategory_GetsASlugFromItsName_UniqueAmongCategoriesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var name = $"Billing & Payments {Guid.NewGuid():N}";

        var first = await KbTestSeeds.CreateCategoryAsync(client, name);
        var second = await KbTestSeeds.CreateCategoryAsync(client, name.ToUpperInvariant());

        var firstSlug = (await KbTestSeeds.GetCategoryAsync(client, first)).GetProperty("slug").GetString()!;
        var secondSlug = (await KbTestSeeds.GetCategoryAsync(client, second)).GetProperty("slug").GetString();
        firstSlug.Should().StartWith("billing-payments-").And.MatchRegex("^[a-z0-9]+(-[a-z0-9]+)*$");
        secondSlug.Should().Be($"{firstSlug}-2");
    }

    /// <summary>A parent category that does not exist is refused with 404 <c>KBCATEGORY_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task CreateRefusesAnUnknownParentCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/knowledgebase/categories", new
        {
            name = $"Category-{Guid.NewGuid():N}",
            description = "A category.",
            isHidden = false,
            parentCategoryId = SupportTestSeeds.NoSuchId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot create a knowledge base category.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/knowledgebase/categories", new
        {
            name = "Anything",
            description = "Anything.",
            isHidden = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
