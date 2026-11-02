using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary><see cref="EventStreamResult"/>: the request always ends, even when a write fails while the producer ignores its token.</summary>
public sealed class EventStreamResultTests
{
    /// <summary>How long a test waits for the request to end before it calls it stuck.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>A response body whose writes fail as a dropped connection's do.</summary>
    private sealed class DroppedConnectionStream : Stream
    {
        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new IOException("The connection was dropped.");
        }

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("The connection was dropped.");
        }
    }

    /// <summary>A producer that ignores its token: it ends only when the test lets it.</summary>
    /// <param name="release">Completed by the test to let the producer end.</param>
    /// <returns>No events.</returns>
    private static async IAsyncEnumerable<StreamedEvent> IgnoresItsToken(Task release)
    {
        await release;
        yield break;
    }

    /// <summary>
    /// A heartbeat write that fails while the producer's step is in flight and ignores cancellation ends the request with
    /// that failure once the settle wait is spent, instead of waiting for the producer for ever.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AFailedWrite_WithAProducerThatIgnoresItsToken_StillEndsTheRequest()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var http = new DefaultHttpContext();
        http.Response.Body = new DroppedConnectionStream();
        var reply = new StreamedReply(IgnoresItsToken(release.Task), TimeSpan.FromMilliseconds(20));
        var result = new EventStreamResult(reply, TimeSpan.FromMilliseconds(100));

        try
        {
            var act = () => result.ExecuteResultAsync(new ActionContext(http, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor())).WaitAsync(Patience);

            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            release.SetResult();
        }
    }
}
