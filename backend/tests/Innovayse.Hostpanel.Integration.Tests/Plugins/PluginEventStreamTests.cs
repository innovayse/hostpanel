using System.Runtime.CompilerServices;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.Endpoints;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginEventStream"/>: what the host lets through from a plugin's stream and how it ends a stream the plugin
/// broke. Every refusal is the <c>error</c> event followed by <c>done</c>, never an exception to the response.
/// </summary>
public sealed class PluginEventStreamTests
{
    /// <summary>The cap the tests translate with, in bytes.</summary>
    private const int Cap = 16;

    /// <summary>The sentence of the <c>error</c> event.</summary>
    private const string Failed = "The plugin failed.";

    /// <summary>A source whose enumerator cannot be created, or whose disposal throws.</summary>
    private sealed class BrokenSource : IAsyncEnumerable<PluginEvent>
    {
        /// <summary>Whether creating the enumerator throws; otherwise disposing it does.</summary>
        private readonly bool _throwOnCreate;

        /// <summary>Initializes the source.</summary>
        /// <param name="throwOnCreate">Whether creating the enumerator throws; otherwise disposing it does.</param>
        public BrokenSource(bool throwOnCreate)
        {
            _throwOnCreate = throwOnCreate;
        }

        /// <inheritdoc/>
        public IAsyncEnumerator<PluginEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (_throwOnCreate)
            {
                throw new InvalidOperationException("secret-detail-of-the-plugin");
            }

            return new ThrowingOnDispose();
        }
    }

    /// <summary>An enumerator over no events whose disposal throws.</summary>
    private sealed class ThrowingOnDispose : IAsyncEnumerator<PluginEvent>
    {
        /// <inheritdoc/>
        public PluginEvent Current => throw new InvalidOperationException();

        /// <inheritdoc/>
        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(false);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            throw new InvalidOperationException("secret-detail-of-the-plugin");
        }
    }

    /// <summary>Events that arrive one by one.</summary>
    /// <param name="events">The events, in order.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> Of(params PluginEvent[] events)
    {
        foreach (var next in events)
        {
            await Task.Yield();
            yield return next;
        }
    }

    /// <summary>A stream that sends one event, then waits for its token to be cancelled and throws as a cancelled plugin does.</summary>
    /// <param name="ct">The token the translation hands the plugin.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<PluginEvent> UntilCancelled([EnumeratorCancellation] CancellationToken ct)
    {
        yield return new PluginEvent("first");
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    /// <summary>Translates a source and collects what the host would write.</summary>
    /// <param name="source">The plugin's events.</param>
    /// <param name="ct">The caller's token.</param>
    /// <returns>The host's events.</returns>
    private static async Task<List<StreamedEvent>> RunAsync(IAsyncEnumerable<PluginEvent> source, CancellationToken ct = default)
    {
        var written = new List<StreamedEvent>();
        await foreach (var next in PluginEventStream.TranslateAsync("plugin-a", source, Failed, NullLogger.Instance, Cap, ct))
        {
            written.Add(next);
        }

        return written;
    }

    /// <summary>Asserts the list ends with the <c>error</c> event and <c>done</c>, and leaks nothing of the plugin's exception.</summary>
    /// <param name="written">What the host wrote.</param>
    private static void AssertEndsWithError(List<StreamedEvent> written)
    {
        written.Should().HaveCountGreaterThanOrEqualTo(2);
        written[^2].Name.Should().Be("error");
        written[^2].Data.Should().Contain("PLUGIN_FAILED").And.Contain(Failed).And.Contain("\"partial\":true");
        written[^1].Name.Should().Be("done");
        written.Should().NotContain(e => e.Data.Contains("secret-detail"));
    }

    /// <summary>A plugin whose enumerator cannot be created still gets the error and done events.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnEnumeratorThatCannotBeCreated_EndsWithTheErrorEvents()
    {
        var written = await RunAsync(new BrokenSource(throwOnCreate: true));

        written.Should().HaveCount(2);
        AssertEndsWithError(written);
    }

    /// <summary>A plugin whose disposal throws after a clean end still gets the error and done events.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnEnumeratorWhoseDisposalThrows_EndsWithTheErrorEvents()
    {
        var written = await RunAsync(new BrokenSource(throwOnCreate: false));

        written.Should().HaveCount(2);
        AssertEndsWithError(written);
    }

    /// <summary>An event named like one of the host's terminal events is refused after the events before it.</summary>
    /// <param name="name">The reserved name, in any case.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("error")]
    [InlineData("done")]
    [InlineData("ERROR")]
    [InlineData("Done")]
    public async Task AReservedEventName_IsRefused(string name)
    {
        var written = await RunAsync(Of(new PluginEvent("ok"), new PluginEvent("spoof", name), new PluginEvent("never")));

        written.Should().HaveCount(3);
        written[0].Data.Should().Be("ok");
        AssertEndsWithError(written);
        written.Should().NotContain(e => e.Data == "spoof");
    }

    /// <summary>Data of exactly the cap passes; one byte more ends the stream with the error event.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnEventOverTheCap_EndsTheStream_AndOneAtTheCapPasses()
    {
        var atCap = await RunAsync(Of(new PluginEvent(new string('a', Cap))));
        var over = await RunAsync(Of(new PluginEvent("ok"), new PluginEvent(new string('a', Cap + 1))));

        atCap.Should().ContainSingle().Which.Data.Should().HaveLength(Cap);
        over.Should().HaveCount(3);
        over[0].Data.Should().Be("ok");
        AssertEndsWithError(over);
    }

    /// <summary>The cap counts UTF-8 bytes, not characters.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheCap_CountsBytes_NotCharacters()
    {
        // Nine characters of two bytes each: eighteen bytes, over the sixteen.
        var written = await RunAsync(Of(new PluginEvent(new string('é', 9))));

        AssertEndsWithError(written);
    }

    /// <summary>A caller that leaves is not an error event: the cancellation reaches the response, which has nobody to answer.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACancellationOnDisconnect_IsNotAnErrorEvent()
    {
        using var disconnect = new CancellationTokenSource();
        var written = new List<StreamedEvent>();

        var act = async () =>
        {
            await foreach (var next in PluginEventStream.TranslateAsync("plugin-a", UntilCancelled(CancellationToken.None), Failed, NullLogger.Instance, Cap, disconnect.Token))
            {
                written.Add(next);
                await disconnect.CancelAsync();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        written.Should().ContainSingle().Which.Data.Should().Be("first");
    }
}
