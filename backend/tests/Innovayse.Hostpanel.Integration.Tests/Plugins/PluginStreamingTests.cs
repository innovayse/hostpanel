using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Hostpanel.Sdk.Plugins.Endpoints;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// A streaming plugin route (<c>PluginReply.Stream</c>) written as <c>text/event-stream</c>, against Postgres and the
/// test-only plugins of <see cref="AppPluginFixture"/>: the events arrive in order in server-sent event framing, a silent
/// stream carries heartbeat comments (the fixture sets the interval to one second), a client that disconnects cancels the
/// plugin, a refusal before the first event keeps its own status and problem body, and the authentication of the other
/// plugin calls is unchanged.
/// </summary>
public sealed class PluginStreamingTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The address of the client area streaming route.</summary>
    private const string ClientStream = "stream";

    /// <summary>How long a test waits for something that should already be on its way before it calls the stream stuck.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginStreamingTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>Opens a client area route and returns as soon as the response headers are in, leaving the body unread.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <param name="content">The body, or <see langword="null"/>.</param>
    /// <returns>The response, to dispose when the test ends; disposing it before the body is read is a disconnect.</returns>
    private async Task<HttpResponseMessage> OpenClientAsync(HttpClient http, HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, $"/api/client/plugins/{_fixture.PluginId}/{path}") { Content = content };
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    /// <summary>Opens an admin area route and returns as soon as the response headers are in.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="path">The path below the plugin's admin prefix.</param>
    /// <returns>The response, to dispose when the test ends.</returns>
    private async Task<HttpResponseMessage> OpenAdminAsync(HttpClient http, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/plugins/{_fixture.PluginId}/ext/{path}");
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    /// <summary>Reads the next line of a stream, failing the test rather than hanging when none comes.</summary>
    /// <param name="reader">The reader over the response body.</param>
    /// <returns>The line without its line break; <see langword="null"/> at the end of the stream.</returns>
    private static async Task<string?> NextLineAsync(StreamReader reader)
    {
        return await reader.ReadLineAsync().WaitAsync(Patience);
    }

    /// <summary>Reads a response body to its end, failing the test rather than hanging when the stream never ends.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The whole body.</returns>
    private static async Task<string> ReadToEndAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadAsStringAsync().WaitAsync(Patience);
    }

    /// <summary>Registers an account holder and makes their client of the plugin host.</summary>
    /// <param name="label">Distinguishes the registered address.</param>
    /// <returns>A client of the plugin host carrying the account holder's token.</returns>
    private async Task<HttpClient> OwnerAsync(string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, label);
        return _fixture.ClientLike(owner.Client);
    }

    /// <summary>A stream of the given events that ends by itself.</summary>
    /// <param name="events">The events, in order.</param>
    /// <returns>The events, produced one by one.</returns>
    private static async IAsyncEnumerable<PluginEvent> Finite(params PluginEvent[] events)
    {
        foreach (var next in events)
        {
            await Task.Yield();
            yield return next;
        }
    }

    /// <summary>A stream that sends one event, then waits for ever, noting when it is cancelled.</summary>
    /// <param name="cancelled">Completed when the stream sees its token cancelled.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> UntilCancelled(
        TaskCompletionSource cancelled, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new PluginEvent("first");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException)
        {
            cancelled.SetResult();
            throw;
        }
    }

    /// <summary>A stream that sends one event and then throws.</summary>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> FailsAfterFirst()
    {
        yield return new PluginEvent("first");
        await Task.Yield();
        throw new InvalidOperationException("secret-detail-of-the-plugin");
    }

    /// <summary>The events arrive in order, framed as server-sent events, with the headers a proxy needs to pass them on.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheEvents_AreWrittenInOrder_AsServerSentEvents()
    {
        var http = await OwnerAsync("stream-order");
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(Finite(
            new PluginEvent("{\"n\":1}", "tick", "7"),
            new PluginEvent("two\nlines"),
            new PluginEvent("last")));

        using var response = await OpenClientAsync(http, HttpMethod.Get, ClientStream);
        var body = await ReadToEndAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        response.Headers.GetValues("X-Accel-Buffering").Should().ContainSingle().Which.Should().Be("no");
        body.Should().Be("event: tick\nid: 7\ndata: {\"n\":1}\n\ndata: two\ndata: lines\n\ndata: last\n\n");
    }

    /// <summary>A POST route streams too, with the body the caller sent, under the same bearer authentication.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APostRoute_StreamsWithTheBodyTheCallerSent()
    {
        var http = await OwnerAsync("stream-post");
        _fixture.Probe.Behaviour = call => PluginReply.Stream(Finite(new PluginEvent(Encoding.UTF8.GetString(call.Body.Span))));

        using var response = await OpenClientAsync(
            http, HttpMethod.Post, ClientStream, new StringContent("ask", Encoding.UTF8, "text/plain"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadToEndAsync(response)).Should().Be("data: ask\n\n");
    }

    /// <summary>A stream that stays silent carries comment lines while it is silent (at least two over two intervals), and the events go on after them.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ASilentStream_CarriesHeartbeatComments()
    {
        var http = await OwnerAsync("stream-heartbeat");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(GatedStream.Open(gate.Task, CancellationToken.None));

        using var response = await OpenClientAsync(http, HttpMethod.Get, ClientStream);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

        (await NextLineAsync(reader)).Should().Be("data: first");
        (await NextLineAsync(reader)).Should().BeEmpty();
        (await NextLineAsync(reader)).Should().Be(": keepalive");
        (await NextLineAsync(reader)).Should().BeEmpty();
        (await NextLineAsync(reader)).Should().Be(": keepalive");
        (await NextLineAsync(reader)).Should().BeEmpty();

        gate.SetResult();

        (await NextLineAsync(reader)).Should().Be("data: last");
    }

    /// <summary>A client that goes away mid stream cancels the plugin: the stream sees its token cancelled.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADisconnect_CancelsThePlugin()
    {
        var http = await OwnerAsync("stream-disconnect");
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(UntilCancelled(cancelled, CancellationToken.None));

        var response = await OpenClientAsync(http, HttpMethod.Get, ClientStream);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        (await NextLineAsync(reader)).Should().Be("data: first");

        // Disposing the response before the stream has ended is the client closing the connection.
        response.Dispose();

        await cancelled.Task.WaitAsync(Patience);
    }

    /// <summary>A plugin that refuses before the first event answers with its own status and problem body, not a stream.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARefusalBeforeTheFirstEvent_KeepsItsStatusAndProblemBody()
    {
        var http = await OwnerAsync("stream-refusal");
        _fixture.Probe.Behaviour = _ => PluginReply.Problem(422, "stream_busy", "Try again later.");

        using var response = await OpenClientAsync(http, HttpMethod.Get, ClientStream);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ErrorBody.ContentType);
        using var document = JsonDocument.Parse(await ReadToEndAsync(response));
        document.RootElement.GetProperty("type").GetString().Should().Be($"plugin.{_fixture.PluginId}.stream_busy");
    }

    /// <summary>A plugin that throws before it returns its stream is the same 502 as for any call, with nothing of the exception.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatThrowsBeforeTheStream_Answers502()
    {
        var http = await OwnerAsync("stream-throws-early");
        _fixture.Probe.Behaviour = _ => throw new InvalidOperationException("secret-detail-of-the-plugin");

        using var response = await OpenClientAsync(http, HttpMethod.Get, ClientStream);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_FAILED);
    }

    /// <summary>A plugin that fails after the stream started ends it with an error event and a done event, and leaks nothing of the exception.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatFailsMidStream_EndsItWithAnErrorEvent()
    {
        var http = await OwnerAsync("stream-throws-late");
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(FailsAfterFirst());

        using var response = await OpenClientAsync(http, HttpMethod.Get, ClientStream);
        var body = await ReadToEndAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().StartWith("data: first\n\nevent: error\ndata: {\"code\":\"PLUGIN_FAILED\",\"message\":");
        body.Should().Contain("\"partial\":true");
        body.Should().EndWith("event: done\ndata: {}\n\n");
        body.Should().NotContain("secret-detail");
        _fixture.LoggedFailureOf(_fixture.PluginId).Should().BeTrue();
    }

    /// <summary>A stream from a route that did not declare streaming is a plugin fault, and the stream is never enumerated.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamFromARouteThatDoesNotStream_Answers502AndIsNeverEnumerated()
    {
        var http = await OwnerAsync("stream-undeclared");
        var enumerated = false;
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(NoteEnumerated(() => enumerated = true));

        using var response = await OpenClientAsync(http, HttpMethod.Get, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_FAILED);
        enumerated.Should().BeFalse();
    }

    /// <summary>A streaming route may still answer with one JSON document.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamingRoute_MayAnswerWithOneJsonDocument()
    {
        var http = await OwnerAsync("stream-json");

        using var response = await OpenClientAsync(http, HttpMethod.Get, ClientStream);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var document = JsonDocument.Parse(await ReadToEndAsync(response));
        document.RootElement.GetProperty("area").GetString().Should().Be("Client");
    }

    /// <summary>Without a session a streaming route is a 401, like every plugin call, and the plugin is not built.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WithoutASession_AStreamingRouteAnswers401()
    {
        using var anonymous = _fixture.Host.CreateClient();

        using var response = await OpenClientAsync(anonymous, HttpMethod.Get, ClientStream);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>The admin area streams the same way, and a disconnect cancels the plugin there too.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnAdminStream_IsWrittenAndCancelledLikeAClientOne()
    {
        using var admin = await _fixture.AdminClientAsync();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(UntilCancelled(cancelled, CancellationToken.None));

        var response = await OpenAdminAsync(admin, "stream");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        (await NextLineAsync(reader)).Should().Be("data: first");

        response.Dispose();

        await cancelled.Task.WaitAsync(Patience);
    }

    /// <summary>An event source that notes when it is enumerated.</summary>
    /// <param name="onEnumerated">Runs when the first event is asked for.</param>
    /// <returns>An empty stream.</returns>
    private static async IAsyncEnumerable<PluginEvent> NoteEnumerated(Action onEnumerated)
    {
        onEnumerated();
        await Task.CompletedTask;
        yield break;
    }
}
