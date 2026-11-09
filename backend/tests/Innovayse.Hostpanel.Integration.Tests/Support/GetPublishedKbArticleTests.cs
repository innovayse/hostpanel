using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/knowledgebase/{id}</c>: an anonymous caller can read a published article; an id
/// naming no row and an id naming an unpublished draft are both refused
/// <c>KBARTICLE_NOT_FOUND</c>, deliberately indistinguishable, so the route cannot
/// be used to map the unpublished backlog one guessed id at a time.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPublishedKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A published article is readable through the public route, with no credential at all, and
    /// carries no editorial flag.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicRoute_ReturnsPublishedArticleAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var anonymous = factory.CreateClient();

        var createResponse = await admin.PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = $"Published-{Guid.NewGuid():N}",
            content = "Visible to everyone.",
            category = "General",
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());
        (await admin.PutAsync($"/api/admin/knowledgebase/{id}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await anonymous.GetAsync($"/api/knowledgebase/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var article = await response.Content.ReadFromJsonAsync<JsonElement>();
        article.TryGetProperty("isPublished", out _).Should().BeFalse();
    }

    /// <summary>
    /// The public route answers the same 404 whether the id names no article at all or names an
    /// unpublished draft.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicRoute_RefusesMissingAndUnpublishedAlikeAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var anonymous = factory.CreateClient();

        var createResponse = await admin.PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = $"Draft-{Guid.NewGuid():N}",
            content = "Not yet public.",
            category = "General",
        });
        var draftId = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var draftResponse = await anonymous.GetAsync($"/api/knowledgebase/{draftId}");
        var missingResponse = await anonymous.GetAsync($"/api/knowledgebase/{SupportTestSeeds.NoSuchId}");

        draftResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(draftResponse, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
        await SupportTestSeeds.AssertCodeAsync(missingResponse, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
    }
}
