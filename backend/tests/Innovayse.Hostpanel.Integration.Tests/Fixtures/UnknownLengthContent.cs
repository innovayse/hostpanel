using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Request content that does not know its own length, so the request carries no <c>Content-Length</c>: a chunked body,
/// which only the reader can bound.
/// </summary>
/// <param name="bytes">The bytes to send.</param>
public sealed class UnknownLengthContent(byte[] bytes) : HttpContent
{
    /// <inheritdoc/>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await stream.WriteAsync(bytes);
    }

    /// <inheritdoc/>
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
