using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The plugin body guards on a real Kestrel socket (<see cref="KestrelBodyHost"/>), where the web server's own per request
/// limit exists and the in-memory test server has none: a chunked body above that default but within the configured cap
/// reaches the reader whole, one byte past the cap answers the host's own 413 (a body the server itself refused has no
/// such code), a body sent to a route that never reads it is bounded by the server, and a form typed body is read by the
/// reader alone. The controls without a guard show that each guard is what makes the difference.
/// </summary>
public sealed class PluginKestrelBodyTests : IClassFixture<KestrelBodyHost>
{
    /// <summary>40 MiB: above the server's default limit (about 28.6 MiB), below the 64 MiB cap.</summary>
    private const long AboveServerDefault = 40L * 1024 * 1024;

    /// <summary>The web server's default per request body limit, in bytes (30,000,000).</summary>
    private const long ServerDefaultLimit = 30_000_000;

    /// <summary>The slack allowed around a limit for what the sender had already put in socket buffers.</summary>
    private const long SocketSlack = 12L * 1024 * 1024;

    /// <summary>The host on a real socket.</summary>
    private readonly KestrelBodyHost _host;

    /// <summary>Initializes the test.</summary>
    /// <param name="host">The host on a real socket.</param>
    public PluginKestrelBodyTests(KestrelBodyHost host)
    {
        _host = host;
    }

    /// <summary>Sends a generated body to a probe.</summary>
    /// <param name="probe">Which probe: <c>guarded</c>, <c>limit-only</c> or <c>unguarded</c>.</param>
    /// <param name="route">The route below the probe, <c>x/echo</c> to read the body or <c>refuse/x</c> to turn it away.</param>
    /// <param name="content">The body.</param>
    /// <param name="mediaType">Its media type.</param>
    /// <returns>The response, or <see langword="null"/> when the server closed the connection before answering.</returns>
    private async Task<HttpResponseMessage?> SendAsync(string probe, string route, GeneratedContent content, string mediaType)
    {
        using var http = _host.CreateClient();
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{probe}/{route}") { Content = content };

        try
        {
            return await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Asserts that the response is the host's own 413 for a body over the cap, not the web server's.</summary>
    /// <param name="response">The response.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertOwn413Async(HttpResponseMessage? response)
    {
        response.Should().NotBeNull("the host answers its own 413 rather than the server closing the connection");
        response!.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
    }

    /// <summary>Asserts that a route which never reads the body answered 404, or that the connection was closed once the server had enough.</summary>
    /// <param name="response">The response, or <see langword="null"/> when the connection was closed.</param>
    private static void AssertNotFoundOrClosed(HttpResponseMessage? response)
    {
        if (response is not null)
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    /// <summary>Asserts that the body was refused: by the host's own 413, or by the web server (a closed connection or its bare 413).</summary>
    /// <param name="response">The response, or <see langword="null"/> when the connection was closed.</param>
    private static void AssertRefused(HttpResponseMessage? response)
    {
        if (response is not null)
        {
            response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        }
    }

    /// <summary>Asserts that the web server itself refused the body: it closed the connection, or answered a 413 that is not the host's.</summary>
    /// <param name="response">The response, or <see langword="null"/> when the connection was closed.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertRefusedByTheServerAsync(HttpResponseMessage? response)
    {
        if (response is not null)
        {
            response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
            (await response.Content.ReadAsStringAsync()).Should().NotContain(HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        }
    }

    /// <summary>Asserts that the response is a 200 for a body of the given length that arrived intact.</summary>
    /// <param name="response">The response.</param>
    /// <param name="length">The expected length.</param>
    /// <returns>The content type the reader saw.</returns>
    private static async Task<string?> AssertReadWholeAsync(HttpResponseMessage? response, long length)
    {
        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var seen = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        seen.RootElement.GetProperty("length").GetInt64().Should().Be(length);
        seen.RootElement.GetProperty("intact").GetBoolean().Should().BeTrue();
        return seen.RootElement.GetProperty("contentType").GetString();
    }

    /// <summary>Control: with no guard, the same body is refused by the web server's own default limit.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unguarded_ABodyAboveTheServerDefault_IsRefusedByTheServer()
    {
        var response = await SendAsync("unguarded", "x/echo", new GeneratedContent(AboveServerDefault, declareLength: false), "application/octet-stream");

        await AssertRefusedByTheServerAsync(response);
    }

    /// <summary>A chunked body above the server's default limit but within the cap is read whole.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AChunkedBodyAboveTheServerDefaultButWithinTheCap_IsReadWhole()
    {
        var response = await SendAsync("guarded", "x/echo", new GeneratedContent(AboveServerDefault, declareLength: false), "application/octet-stream");

        await AssertReadWholeAsync(response, AboveServerDefault);
    }

    /// <summary>A declared body above the server's default limit but within the cap is read whole.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredBodyAboveTheServerDefaultButWithinTheCap_IsReadWhole()
    {
        var response = await SendAsync("guarded", "x/echo", new GeneratedContent(AboveServerDefault, declareLength: true), "application/octet-stream");

        await AssertReadWholeAsync(response, AboveServerDefault);
    }

    /// <summary>A chunked body of exactly the cap is read whole, and one byte more answers the host's own 413.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AChunkedBodyAtTheCapPasses_AndOneByteMoreAnswersTheHostsOwn413()
    {
        var atCap = await SendAsync(
            "guarded", "x/echo", new GeneratedContent(PluginApiOptions.MaxBodyBytesCeiling, declareLength: false), "application/octet-stream");
        var overCap = await SendAsync(
            "guarded", "x/echo", new GeneratedContent(PluginApiOptions.MaxBodyBytesCeiling + 1L, declareLength: false), "application/octet-stream");

        await AssertReadWholeAsync(atCap, PluginApiOptions.MaxBodyBytesCeiling);
        await AssertOwn413Async(overCap);
    }

    /// <summary>A declared length one byte over the cap is refused with the host's own 413, without the body being read.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredLengthOverTheCap_AnswersTheHostsOwn413()
    {
        var content = new GeneratedContent(PluginApiOptions.MaxBodyBytesCeiling + 1L, declareLength: true);

        var response = await SendAsync("guarded", "x/echo", content, "application/octet-stream");

        await AssertOwn413Async(response);
    }

    /// <summary>
    /// A huge body sent to a route that never reads it (an undeclared route, a caller turned away) is bounded by
    /// <see cref="PluginApiOptions.ServerBodyLimit"/>, not by the server's default: the sender gets past the default
    /// (which the control route without the limit filter stops at) and stops near the limit, which is above it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnOversizeBodyToARouteThatNeverReadsIt_IsBoundedByTheServerBodyLimit()
    {
        var limit = new PluginApiOptions { MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling }.ServerBodyLimit;
        var control = new GeneratedContent(300L * 1024 * 1024, declareLength: false);
        var guarded = new GeneratedContent(300L * 1024 * 1024, declareLength: false);

        var controlResponse = await SendAsync("unguarded", "refuse/x", control, "application/octet-stream");
        var guardedResponse = await SendAsync("guarded", "refuse/x", guarded, "application/octet-stream");

        AssertNotFoundOrClosed(controlResponse);
        AssertNotFoundOrClosed(guardedResponse);
        control.Written.Should().BeLessThan(ServerDefaultLimit + SocketSlack, "the control route is bounded by the server's default limit");
        guarded.Written.Should().BeLessThan(limit + SocketSlack, "the guarded route is bounded by the server body limit");
        guarded.Written.Should().BeGreaterThan(ServerDefaultLimit + SocketSlack, "the sender got past what the server's default would have allowed");
        guarded.Written.Should().BeGreaterThan(limit - SocketSlack, "the bound is the server body limit, not the lower default");
        guarded.Written.Should().BeGreaterThan(control.Written + (limit - ServerDefaultLimit) / 2);
    }

    /// <summary>
    /// A chunked body of many small chunks (256 bytes each, so the framing the server counts is a larger share of it) one
    /// byte over the cap is refused, by the host's own 413 or by the server, and the sender is stopped near the limit.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AChunkedBodyOfSmallChunksOverTheCap_IsRefusedAndBounded()
    {
        var limit = new PluginApiOptions { MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling }.ServerBodyLimit;
        var content = new GeneratedContent(PluginApiOptions.MaxBodyBytesCeiling + 1L, declareLength: false, blockBytes: 256);

        var response = await SendAsync("guarded", "x/echo", content, "application/octet-stream");

        AssertRefused(response);
        content.Written.Should().BeLessThan(limit + SocketSlack);
    }

    /// <summary>A urlencoded body above the server's default limit but within the cap is read by the reader alone, whole.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AUrlencodedBodyWithinTheCap_IsReadWhole()
    {
        var response = await SendAsync(
            "guarded", "x/echo", new GeneratedContent(AboveServerDefault, declareLength: false), "application/x-www-form-urlencoded");

        var contentType = await AssertReadWholeAsync(response, AboveServerDefault);

        contentType.Should().Be("application/x-www-form-urlencoded");
    }

    /// <summary>Control: without the form guard, MVC reads the urlencoded body first and the same request fails.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WithoutTheFormGuard_AUrlencodedBodyIsNotReadWhole()
    {
        var content = new GeneratedContent(AboveServerDefault, declareLength: false);

        var response = await SendAsync("limit-only", "x/echo", content, "application/x-www-form-urlencoded");

        // MVC's form reader gives up on the body part way (the server then closes the connection, so there is no
        // response to read), which is what leaves the plugin an empty body: the outcome is that the sender is stopped
        // well before the end, and that no 200 came back.
        content.Written.Should().BeLessThan(AboveServerDefault - SocketSlack, "the form reader refused the body part way");
        if (response is not null)
        {
            response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        }
    }

    /// <summary>A form typed body one byte over the cap answers the host's own 413, urlencoded or multipart.</summary>
    /// <param name="mediaType">The form's media type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("multipart/form-data; boundary=b")]
    public async Task AFormBodyOverTheCap_AnswersTheHostsOwn413(string mediaType)
    {
        var response = await SendAsync(
            "guarded", "x/echo", new GeneratedContent(PluginApiOptions.MaxBodyBytesCeiling + 1L, declareLength: false), mediaType);

        await AssertOwn413Async(response);
    }
}
