using System.Net;
using System.Runtime.CompilerServices;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Host;
using Innovayse.SDK.Plugins.Store;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// A streamed plugin reply owns its own dependency scope (#851): the store and the host API the plugin received stay usable
/// between events after the use case has returned, through the client and the admin door alike, and the scope, with its data
/// context, is disposed once the stream ended and once the client left. Before the fix every store read inside a stream
/// threw <see cref="ObjectDisposedException"/> on the use case's disposed context.
/// </summary>
public sealed class PluginStreamScopeTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The address of the streaming route, in both areas.</summary>
    private const string StreamRoute = "stream";

    /// <summary>The store key the streams write and read back.</summary>
    private const string Key = "chat:title";

    /// <summary>How long a test waits for something that should already be on its way before it calls the stream stuck.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginStreamScopeTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>
    /// The events of a chat turn: each one is produced after the store or the host API was used, all of it after the use
    /// case returned. The profile event says whether the host knew the caller's profile.
    /// </summary>
    /// <param name="store">The plugin's store, as the call gave it.</param>
    /// <param name="host">The host API, as the call gave it.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> UsesStoreAndHost(
        IPluginStore store, IPluginHostApi host, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new PluginEvent("start");
        await store.SetAsync(Key, "Eva Bakery", ct);
        yield return new PluginEvent("stored");
        var profile = await host.GetProfileAsync(ct);
        yield return new PluginEvent(profile is null ? "no-profile" : "profile");
        var title = await store.GetAsync(Key, ct);
        yield return new PluginEvent(title ?? "missing");
    }

    /// <summary>A stream that writes to its store, sends one event, then waits until it is cancelled.</summary>
    /// <param name="store">The plugin's store, as the call gave it.</param>
    /// <param name="cancelled">Completed when the stream sees its token cancelled.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> WritesThenWaits(
        IPluginStore store, TaskCompletionSource cancelled, [EnumeratorCancellation] CancellationToken ct)
    {
        await store.SetAsync(Key, "draft", ct);
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

    /// <summary>Waits until the store's scope has been disposed, which a read on it then reports.</summary>
    /// <param name="store">The store the plugin received.</param>
    /// <returns>A task that completes once a read throws <see cref="ObjectDisposedException"/>; the test fails after the patience.</returns>
    private static async Task WaitUntilDisposedAsync(IPluginStore store)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await store.GetAsync(Key, CancellationToken.None);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        throw new TimeoutException("The streamed call's scope was not disposed.");
    }

    /// <summary>The single call the probe saw.</summary>
    /// <returns>The call.</returns>
    private PluginCall OnlyCall()
    {
        return _fixture.Probe.Calls.Should().ContainSingle().Subject;
    }

    /// <summary>
    /// A client stream that writes and reads its store and asks the host API between events succeeds end to end, and the
    /// scope it ran on is disposed once the stream has ended.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AClientStream_UsesStoreAndHostBetweenEvents_ThenItsScopeIsDisposed()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "stream-scope");
        var http = _fixture.ClientLike(owner.Client);
        _fixture.Probe.Behaviour = call => PluginReply.Stream(UsesStoreAndHost(call.Store, call.Host, CancellationToken.None));

        using var response = await http.GetAsync($"/api/client/plugins/{_fixture.PluginId}/{StreamRoute}");
        var body = await response.Content.ReadAsStringAsync().WaitAsync(Patience);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("data: start\n\ndata: stored\n\ndata: profile\n\ndata: Eva Bakery\n\n");
        _fixture.LoggedFailureOf(_fixture.PluginId).Should().BeFalse();
        await WaitUntilDisposedAsync(OnlyCall().Store);
    }

    /// <summary>An admin stream, whose store is plugin wide and whose host API knows no client, works the same way.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnAdminStream_UsesStoreAndHostBetweenEvents_ThenItsScopeIsDisposed()
    {
        var http = await _fixture.AdminClientAsync();
        _fixture.Probe.Behaviour = call => PluginReply.Stream(UsesStoreAndHost(call.Store, call.Host, CancellationToken.None));

        using var response = await http.GetAsync($"/api/admin/plugins/{_fixture.PluginId}/ext/{StreamRoute}");
        var body = await response.Content.ReadAsStringAsync().WaitAsync(Patience);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("data: start\n\ndata: stored\n\ndata: no-profile\n\ndata: Eva Bakery\n\n");
        await WaitUntilDisposedAsync(OnlyCall().Store);
    }

    /// <summary>A client that leaves mid stream cancels the plugin, and the stream's scope is disposed after that.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AClientThatLeaves_CancelsTheStream_ThenItsScopeIsDisposed()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "stream-scope-leave");
        var http = _fixture.ClientLike(owner.Client);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fixture.Probe.Behaviour = call => PluginReply.Stream(WritesThenWaits(call.Store, cancelled, CancellationToken.None));

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/client/plugins/{_fixture.PluginId}/{StreamRoute}");
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync()))
        {
            (await reader.ReadLineAsync().WaitAsync(Patience)).Should().Be("data: first");

            // Disposing the response before the stream has ended is the client closing the connection.
            response.Dispose();
        }

        await cancelled.Task.WaitAsync(Patience);
        await WaitUntilDisposedAsync(OnlyCall().Store);
    }

    /// <summary>A streaming route that answers with one JSON document keeps working; its scope ends with the call.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamingRouteAnsweringJson_UsesItsStore_ThenItsScopeIsDisposed()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "stream-scope-json");
        var http = _fixture.ClientLike(owner.Client);
        _fixture.Probe.AsyncBehaviour = async (call, ct) =>
        {
            await call.Store.SetAsync(Key, "json", ct);
            return PluginReply.Json(new { title = await call.Store.GetAsync(Key, ct) });
        };

        using var response = await http.GetAsync($"/api/client/plugins/{_fixture.PluginId}/{StreamRoute}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("json");
        await WaitUntilDisposedAsync(OnlyCall().Store);
    }
}
