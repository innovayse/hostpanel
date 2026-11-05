using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.API.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// The generated OpenAPI document is served in the Testing environment and describes both halves of
/// the surface: the admin routes and the anonymous public routes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SwaggerDocumentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The document is served and lists admin and public paths, tagged by area.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Document_IsServed_AndListsAdminAndPublicPathsAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(SwaggerRegistration.DocumentPath);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        paths.Should().Contain(path => path.StartsWith("/api/admin/", StringComparison.Ordinal));
        paths.Should().Contain(path => path.StartsWith("/api/public/", StringComparison.Ordinal));
        paths.Should().Contain("/health");

        var tags = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .SelectMany(operation => operation.Value.GetProperty("tags").EnumerateArray())
            .Select(tag => tag.GetString())
            .Distinct()
            .ToList();
        tags.Should().Contain([SwaggerRegistration.AdminTag, SwaggerRegistration.PublicTag, SwaggerRegistration.AuthTag]);
    }

    /// <summary>The operations any signed-in user may call carry the Member tag, not the Admin one.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Document_TagsOperationsOpenToAnySignedInUserAsMemberAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(SwaggerRegistration.DocumentPath);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        (string Path, string Method)[] memberOperations =
        [
            ("/api/me/2fa/setup", "post"),
            ("/api/me/2fa/enable", "post"),
            ("/api/me/2fa/disable", "post"),
            ("/api/reference/countries", "get"),
            ("/api/announcements/published", "get"),
        ];

        foreach (var (path, method) in memberOperations)
        {
            var tags = paths.GetProperty(path).GetProperty(method).GetProperty("tags").EnumerateArray()
                .Select(tag => tag.GetString())
                .ToList();
            tags.Should().Equal([SwaggerRegistration.MemberTag], because: $"{method.ToUpperInvariant()} {path} is open to any signed-in user");
        }
    }

    /// <summary>The sign-in answers 200 with a token or with a second factor challenge, and the document shows both bodies.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Document_DescribesBothBodiesOfTheSignInOkAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(SwaggerRegistration.DocumentPath);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ok = document.RootElement.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var variants = ok.GetProperty("oneOf").EnumerateArray()
            .Select(variant => variant.GetProperty("$ref").GetString())
            .ToList();

        variants.Should().HaveCount(2);
        variants.Should().Contain(reference => reference!.EndsWith(".SessionIssuedDto", StringComparison.Ordinal));
        variants.Should().Contain(reference => reference!.EndsWith(".TwoFactorPendingDto", StringComparison.Ordinal));
    }

    /// <summary>Failures in the document are described by the one error body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Document_DescribesTheErrorBodyAsync()
    {
        var client = factory.CreateClient();

        var text = await client.GetStringAsync(SwaggerRegistration.DocumentPath);

        text.Should().Contain("ErrorResponse");
    }
}
