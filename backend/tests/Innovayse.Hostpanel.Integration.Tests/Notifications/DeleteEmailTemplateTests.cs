using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>DeleteEmailTemplate</c> use case: <c>DELETE /api/admin/email-templates/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteEmailTemplateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A deleted template can no longer be read back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheTemplate()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync(
            "/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody($"test-{Guid.NewGuid():N}"));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var deleted = await client.DeleteAsync($"/api/admin/email-templates/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync($"/api/admin/email-templates/{id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>An id that names no template answers 404 with <c>EMAIL_TEMPLATE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesNotFound()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/admin/email-templates/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await NotificationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/admin/email-templates/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
