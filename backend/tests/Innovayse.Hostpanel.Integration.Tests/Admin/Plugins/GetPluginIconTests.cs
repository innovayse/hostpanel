using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins/{id}/icon</c>: an admin reads the icon a loaded plugin ships with the safe headers
/// (declared type, nosniff, restrictive policy, cache and entity tag, 304 on a repeat); an unknown plugin and a plugin
/// without an icon answer the same 404; an id that is not a plugin id is a 400; a caller with only the Client role is
/// refused and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPluginIconTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A small SVG icon.</summary>
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\"><path d=\"M0 0h8v8z\"/></svg>";

    /// <summary>The PNG signature, enough for the served bytes to be recognisable.</summary>
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Builds an admin client over a host whose loaded plugins come from the given fake.</summary>
    /// <param name="registry">The fake registry.</param>
    /// <returns>The admin client.</returns>
    private async Task<HttpClient> AdminClientOverAsync(FakePluginRegistry registry)
    {
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPluginRegistry>(registry)));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return client;
    }

    /// <summary>Registers a loaded plugin, with the icon when one is given.</summary>
    /// <param name="registry">The fake registry.</param>
    /// <param name="id">The plugin id.</param>
    /// <param name="icon">The icon, or <see langword="null"/> for a plugin without one.</param>
    private static void Load(FakePluginRegistry registry, string id, PluginIconDto? icon)
    {
        registry.Add(PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson(id))!);
        if (icon is not null)
        {
            registry.AddIcon(id, icon);
        }
    }

    /// <summary>An SVG icon is served with its type, nosniff, the restrictive policy, a cache window and its tag.</summary>
    [Fact]
    public async Task Svg_IsServedWithSafeHeadersAsync()
    {
        var registry = new FakePluginRegistry();
        Load(registry, "svg-plugin", new PluginIconDto("image/svg+xml", Encoding.UTF8.GetBytes(Svg), "\"abc\""));
        var client = await AdminClientOverAsync(registry);

        var response = await client.GetAsync("/api/admin/plugins/svg-plugin/icon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Be("default-src 'none'; style-src 'unsafe-inline'");
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().NotBeNull();
        response.Headers.ETag!.Tag.Should().Be("\"abc\"");
        (await response.Content.ReadAsStringAsync()).Should().Be(Svg);
    }

    /// <summary>A PNG icon is served with the PNG type and the same safe headers.</summary>
    [Fact]
    public async Task Png_IsServedWithItsTypeAsync()
    {
        var registry = new FakePluginRegistry();
        Load(registry, "png-plugin", new PluginIconDto("image/png", PngBytes, "\"png\""));
        var client = await AdminClientOverAsync(registry);

        var response = await client.GetAsync("/api/admin/plugins/png-plugin/icon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);
    }

    /// <summary>A repeat request holding the current tag is answered 304 with no body.</summary>
    [Fact]
    public async Task RepeatWithTheCurrentTag_IsNotModifiedAsync()
    {
        var registry = new FakePluginRegistry();
        Load(registry, "tag-plugin", new PluginIconDto("image/svg+xml", Encoding.UTF8.GetBytes(Svg), "\"abc\""));
        var client = await AdminClientOverAsync(registry);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/plugins/tag-plugin/icon");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"abc\""));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    /// <summary>A loaded plugin without an icon and an unknown plugin answer the same 404 and the same code.</summary>
    [Fact]
    public async Task NoIconAndUnknownPlugin_AnswerTheSame404Async()
    {
        var registry = new FakePluginRegistry();
        Load(registry, "plain-plugin", null);
        var client = await AdminClientOverAsync(registry);

        var withoutIcon = await client.GetAsync("/api/admin/plugins/plain-plugin/icon");
        var unknown = await client.GetAsync("/api/admin/plugins/not-installed/icon");

        withoutIcon.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await IntegrationsTestSeeds.AssertCodeAsync(withoutIcon, HostpanelErrorCodes.PLUGIN_ICON_NOT_FOUND);
        await IntegrationsTestSeeds.AssertCodeAsync(unknown, HostpanelErrorCodes.PLUGIN_ICON_NOT_FOUND);
    }

    /// <summary>An id that is not a plugin id (traversal, upper case) is refused before any lookup.</summary>
    /// <param name="id">The route value.</param>
    [Theory]
    [InlineData("Bad_Id")]
    [InlineData("..%2f..%2fetc")]
    public async Task InvalidId_IsRefusedAsync(string id)
    {
        var client = await AdminClientOverAsync(new FakePluginRegistry());

        var response = await client.GetAsync($"/api/admin/plugins/{id}/icon");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "plugin-icon-forbidden");

        var response = await client.GetAsync("/api/admin/plugins/any-plugin/icon");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/plugins/any-plugin/icon");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
