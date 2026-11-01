using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The plugin door against hostile requests, through the real pipeline and the real router: odd path spellings (dot
/// segments, encoded separators, backslashes, empty segments), methods no route declares, a method override header and a
/// call cancelled while its body is read. In every case the plugin is only reached through a route its manifest declares,
/// or not at all. The requests are sent to the test server with the path set as the server would hand it over (decoded
/// once, dot segments untouched), because an HTTP client would tidy the path before it left.
/// </summary>
public sealed class PluginRouteSafetyTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginRouteSafetyTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>Registers an account holder and returns the authorization header of their bearer token.</summary>
    /// <param name="label">Distinguishes the registered address.</param>
    /// <returns>The header value, such as <c>Bearer abc</c>.</returns>
    private async Task<string> BearerAsync(string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, label);
        return owner.Client.DefaultRequestHeaders.Authorization!.ToString();
    }

    /// <summary>Sends a request to the test server with the path exactly as given, and reads the answer.</summary>
    /// <param name="bearer">The authorization header value.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The full request path, as the server would hand it to the router.</param>
    /// <param name="configure">Adds more to the request, or <see langword="null"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The status and the body text.</returns>
    private async Task<(int Status, string Body)> SendAsync(
        string bearer, string method, string path, Action<HttpContext>? configure = null, CancellationToken ct = default)
    {
        var context = await _fixture.Host.Server.SendAsync(
            http =>
            {
                http.Request.Method = method;
                http.Request.Path = new PathString(path);
                http.Request.Headers.Authorization = bearer;
                configure?.Invoke(http);
            },
            ct);

        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync(ct));
    }

    /// <summary>The path of a client area plugin route.</summary>
    /// <param name="below">The path below the plugin's prefix.</param>
    /// <returns>The full path.</returns>
    private string PathOf(string below) => $"/api/client/plugins/{_fixture.PluginId}/{below}";

    /// <summary>
    /// A path that is not one of the declared templates answers 404 and no plugin code runs: dot segments are never resolved
    /// into another route, an empty segment matches no parameter, and an encoded separator never splits a segment.
    /// </summary>
    /// <param name="below">A path below the plugin's prefix.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("items/../invoices/1")]
    [InlineData("../ping")]
    [InlineData("items/%2e%2e/invoices/1")]
    [InlineData("items/%2E%2E/%2E%2E/ping")]
    [InlineData("items//x")]
    [InlineData("/ping")]
    [InlineData("ping/")]
    [InlineData("items/a/b")]
    [InlineData("items/a%2Fb/c")]
    [InlineData("items\\..\\invoices\\1")]
    [InlineData("ping\\")]
    public async Task APathThatIsNotADeclaredTemplate_Answers404WithoutRunningThePlugin(string below)
    {
        var bearer = await BearerAsync("unsafe-path");

        var (status, body) = await SendAsync(bearer, "GET", PathOf(below));

        status.Should().Be(StatusCodes.Status404NotFound);
        body.Should().Contain(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
        _fixture.Probe.Calls.Should().BeEmpty();
    }

    /// <summary>
    /// A dot segment or an encoded separator inside the one segment a parameter takes is not resolved by the host: the
    /// plugin receives it as the literal text of that route value, and only through the declared route. The value is
    /// untrusted input, which is why the document tells a plugin never to use it as a file name or an address.
    /// </summary>
    /// <param name="segment">The text of the parameter segment, as the router sees it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("%2e%2e")]
    [InlineData("a%2Fb")]
    [InlineData("a\\b")]
    [InlineData("a%00b")]
    public async Task ASuspiciousSegmentInAParameter_ArrivesAsLiteralTextOnTheDeclaredRoute(string segment)
    {
        var bearer = await BearerAsync("literal-value");

        var (status, body) = await SendAsync(bearer, "GET", PathOf($"items/{segment}"));

        status.Should().Be(StatusCodes.Status200OK);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("route").GetString().Should().Be("/items/{name}");
        document.RootElement.GetProperty("routeValues").GetProperty("name").GetString().Should().Be(segment);
    }

    /// <summary>A method the door does not map is refused by the router before the host or the plugin sees the request.</summary>
    /// <param name="method">A method no plugin route declares.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("CONNECT")]
    public async Task AMethodNoRouteDeclares_IsRefusedWithoutRunningThePlugin(string method)
    {
        var bearer = await BearerAsync("undeclared-http-method");

        var (status, _) = await SendAsync(bearer, method, PathOf("ping"));

        status.Should().BeOneOf(StatusCodes.Status404NotFound, StatusCodes.Status405MethodNotAllowed);
        _fixture.Probe.Constructed.Should().Be(0);
        _fixture.Probe.Calls.Should().BeEmpty();
    }

    /// <summary>
    /// A method override header changes nothing: the route is matched on the real method, so a request cannot reach a route
    /// by naming another method in a header.
    /// </summary>
    /// <param name="real">The method the request is sent with.</param>
    /// <param name="path">A path declared only for another method.</param>
    /// <param name="overridden">The method the header asks for.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("POST", "ping", "GET")]
    [InlineData("GET", "echo", "POST")]
    [InlineData("POST", "orders", "GET")]
    [InlineData("DELETE", "ping", "GET")]
    public async Task AMethodOverrideHeader_IsIgnored(string real, string path, string overridden)
    {
        var bearer = await BearerAsync("method-override");

        foreach (var header in new[] { "X-HTTP-Method-Override", "X-HTTP-Method", "X-Method-Override" })
        {
            var (status, body) = await SendAsync(bearer, real, PathOf(path), http => http.Request.Headers[header] = overridden);

            status.Should().Be(StatusCodes.Status404NotFound);
            body.Should().Contain(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        _fixture.Probe.Constructed.Should().Be(0);
        _fixture.Probe.Calls.Should().BeEmpty();
    }

    /// <summary>
    /// A caller who goes away while the host is still reading the body stops the call where it is: no plugin is built, no
    /// audit line is written, and the plugin is never asked (the host's own message-bus log of the aborted call is not the plugin's doing and is not judged here).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACallCancelledWhileTheBodyIsRead_LeavesNothingBehind()
    {
        var bearer = await BearerAsync("cancelled-call");
        var body = new HangingRequestBody();
        using var cancel = new CancellationTokenSource();

        var sending = SendAsync(
            bearer,
            "POST",
            PathOf("echo"),
            http =>
            {
                http.Request.Body = body;
                http.Request.ContentType = "application/octet-stream";
            },
            cancel.Token);
        await body.Started.WaitAsync(TimeSpan.FromSeconds(30));
        await cancel.CancelAsync();

        // The call ends in a cancellation, not in an answer: the host does not turn the caller leaving into a failure.
        try
        {
            await sending;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            // The expected end of a call that was cancelled: the server had finished with it, so the test server reports the
            // aborted request when its answer is read.
        }

        _fixture.Probe.Constructed.Should().Be(0);
        _fixture.Probe.Calls.Should().BeEmpty();
        (await _fixture.ActivityOfAsync(_fixture.PluginId)).Should().BeEmpty();
    }
}
