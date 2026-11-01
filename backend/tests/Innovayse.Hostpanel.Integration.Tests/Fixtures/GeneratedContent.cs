using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Request content of a given length made on the fly, so a test can send hundreds of megabytes without holding them, and
/// that counts how many bytes it managed to hand to the connection: a server that refuses the body stops the sender early.
/// </summary>
/// <param name="length">How many bytes the body has.</param>
/// <param name="declareLength">Whether the request carries a <c>Content-Length</c>; otherwise it is sent chunked.</param>
public sealed class GeneratedContent(long length, bool declareLength) : HttpContent
{
    /// <summary>The size of one write.</summary>
    private const int BlockBytes = 65_536;

    /// <summary>How many bytes have been written to the connection.</summary>
    private long _written;

    /// <summary>Gets how many bytes were written to the connection before the send ended.</summary>
    public long Written => Interlocked.Read(ref _written);

    /// <inheritdoc/>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var block = new byte[BlockBytes];
        Array.Fill(block, (byte)'x');

        for (var remaining = length; remaining > 0;)
        {
            var size = (int)Math.Min(remaining, BlockBytes);
            await stream.WriteAsync(block.AsMemory(0, size));
            remaining -= size;
            Interlocked.Add(ref _written, size);
        }
    }

    /// <inheritdoc/>
    protected override bool TryComputeLength(out long contentLength)
    {
        contentLength = declareLength ? length : 0;
        return declareLength;
    }
}
