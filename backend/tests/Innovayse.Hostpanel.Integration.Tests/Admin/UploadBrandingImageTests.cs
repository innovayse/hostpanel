using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>POST /api/admin/settings/branding/{kind}</c>: a genuinely decodable image is re-encoded,
/// stored and answers with its URL, bytes that do not decode as an image are refused
/// <c>BRANDING_IMAGE_INVALID</c>, an unrecognised kind is refused as a validation failure, a
/// caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UploadBrandingImageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A minimal, genuinely decodable 1x1 transparent PNG: the branding processor decodes the
    /// bytes for real, so a file that is merely named <c>.png</c> is not enough to exercise the
    /// success path.
    /// </summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>A genuinely decodable image is re-encoded, stored, and answers with its URL.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ValidPng_ReturnsTheStoredUrlAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/admin/settings/branding/logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("url").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("assets").GetArrayLength().Should().BeGreaterThan(0);
    }

    /// <summary>Bytes that do not decode as an image are refused with <c>BRANDING_IMAGE_INVALID</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UndecodableBytes_RefusesInvalidAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("not an image"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/admin/settings/branding/logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
    }

    /// <summary>
    /// A TIFF (and so BigTIFF) container is refused <c>BRANDING_IMAGE_INVALID</c> on its magic
    /// bytes, before any image decoder sees it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TiffHeader_RefusesInvalidAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        using var content = new MultipartFormDataContent();
        // Little-endian TIFF magic, IFD offset 8, then padding.
        byte[] tiff = [0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        var fileContent = new ByteArrayContent(tiff);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/admin/settings/branding/logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.BRANDING_IMAGE_INVALID);
    }

    /// <summary>
    /// Bytes over the deployment's configured ceiling (5 MiB, below the controller's 6 MiB
    /// request-size cap) are refused with <c>BRANDING_IMAGE_TOO_LARGE</c> before anything is
    /// decoded.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OverTheConfiguredCeiling_RefusesTooLargeAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        using var content = new MultipartFormDataContent();
        var oversized = new byte[(5 * 1024 * 1024) + 1024];
        var fileContent = new ByteArrayContent(oversized);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/admin/settings/branding/logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.BRANDING_IMAGE_TOO_LARGE);
    }

    /// <summary>A kind the route does not recognise is refused as a validation failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownKind_RefusesAsValidationFailureAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/admin/settings/branding/bogus", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A numeric kind binds to the enum but names no member, so the validator refuses it before it can
    /// become a storage directory.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NumericKindOutsideTheEnum_RefusesAsValidationFailureAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/admin/settings/branding/7", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "upload-branding-forbidden");
        using var content = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/admin/settings/branding/logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A caller with no credential is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        using var content = new MultipartFormDataContent();

        var response = await factory.CreateClient().PostAsync("/api/admin/settings/branding/logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
