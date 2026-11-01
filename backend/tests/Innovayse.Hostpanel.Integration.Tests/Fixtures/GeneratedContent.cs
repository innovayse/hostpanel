using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Request content of a given length made on the fly, so a test can send hundreds of megabytes without holding them, and
/// that counts how many bytes it managed to hand to the connection: a server that refuses the body stops the sender early.
/// </summary>
/// <param name="length">How many bytes the body has.</param>
/// <param name="declareLength">Whether the request carries a <c>Content-Length</c>; otherwise it is sent chunked.</param>
/// <param name="blockBytes">The size of one write; a chunked body is sent as one chunk per write, so a small value makes many small chunks.</param>
public sealed class GeneratedContent(long length, bool declareLength, int blockBytes = GeneratedContent.DefaultBlockBytes) : HttpContent
{
    /// <summary>The size of one write unless the sender asks for another.</summary>
    public const int DefaultBlockBytes = 65_536;

    /// <summary>How many bytes have been written to the connection.</summary>
    private long _written;

    /// <summary>Gets how many bytes were written to the connection before the send ended.</summary>
    public long Written => Interlocked.Read(ref _written);

    /// <inheritdoc/>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var block = new byte[blockBytes];
        Array.Fill(block, (byte)'x');

        for (var remaining = length; remaining > 0;)
        {
            var size = (int)Math.Min(remaining, blockBytes);
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
