using Innovayse.Hostpanel.Application.Common.Settings;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The one place a plugin route's request body is read: whole or not at all, within the configured cap, into a single
/// buffer the plugin is handed a slice of.
/// </summary>
public static class PluginBodyReader
{
    /// <summary>The most the buffer is sized to up front from a declared length; it grows from there as bytes arrive.</summary>
    private const int InitialBufferBytes = 65_536;

    /// <summary>Reads the request body, within the cap.</summary>
    /// <param name="input">The request.</param>
    /// <param name="cap">The most bytes the body may have.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The body bytes (a slice of the one buffer read), empty when there is none; <see langword="null"/> when the request
    /// declares more than the cap or sends more than it, in which case no more than the cap and one chunk was read.
    /// </returns>
    public static async Task<ReadOnlyMemory<byte>?> ReadAsync(PluginHttpInput input, int cap, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.ContentLength > cap)
        {
            return null;
        }

        // The one copy of the body: the plugin is handed a slice of this buffer, never a second array made with ToArray.
        // A declared length is only a claim until the bytes arrive, so it never sizes the buffer beyond a small start:
        // a caller who declares the whole cap and sends nothing must not be able to make the host allocate it. The
        // buffer grows (doubling) with what is really read.
        var initial = (int)Math.Min(Math.Max(0L, input.ContentLength ?? 0L), InitialBufferBytes);
        using var buffer = new MemoryStream(initial);
        var chunk = new byte[PluginApiOptions.ReadChunkBytes];
        int read;
        while ((read = await input.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > cap)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
    }
}
