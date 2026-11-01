using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginBodyReader"/> on its own: whole or not at all, within the cap, and the declared length is only a
/// claim that never decides how much is read.
/// </summary>
public sealed class PluginBodyReaderTests
{
    /// <summary>The cap the reader is given.</summary>
    private const int Cap = 20_000;

    /// <summary>Makes a request whose body is the given stream and whose declared length is as given.</summary>
    /// <param name="body">The stream the body is read from.</param>
    /// <param name="declared">The declared length, or <see langword="null"/> when there is none.</param>
    /// <returns>The request.</returns>
    private static PluginHttpInput Input(Stream body, long? declared)
    {
        return new PluginHttpInput(
            PluginRouteMethod.Post, "x", new Dictionary<string, IReadOnlyList<string>>(), body, "application/octet-stream", declared);
    }

    /// <summary>Makes a request carrying the given number of bytes, declaring their real count.</summary>
    /// <param name="size">How many bytes.</param>
    /// <returns>The request.</returns>
    private static PluginHttpInput InputOfSize(int size)
    {
        return Input(new MemoryStream(new byte[size]), size);
    }

    /// <summary>An empty body reads as empty, not as a refusal.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ANoByteBody_IsEmpty()
    {
        var result = await PluginBodyReader.ReadAsync(InputOfSize(0), Cap, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.Length.Should().Be(0);
    }

    /// <summary>A body of exactly the cap is read whole, across several chunks.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABodyOfExactlyTheCap_IsReadWhole()
    {
        var result = await PluginBodyReader.ReadAsync(InputOfSize(Cap), Cap, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.Length.Should().Be(Cap);
    }

    /// <summary>A body one byte over the cap is refused when it declares its length, without a byte being read.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredBodyOneByteOverTheCap_IsRefusedWithoutReadingIt()
    {
        var stream = new MemoryStream(new byte[Cap + 1]);

        var result = await PluginBodyReader.ReadAsync(Input(stream, Cap + 1), Cap, CancellationToken.None);

        result.Should().BeNull();
        stream.Position.Should().Be(0);
    }

    /// <summary>A body one byte over the cap that declares nothing is refused as the bytes arrive, at most one chunk past the cap.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUndeclaredBodyOneByteOverTheCap_IsRefused()
    {
        var stream = new MemoryStream(new byte[Cap + 1]);

        var result = await PluginBodyReader.ReadAsync(Input(stream, null), Cap, CancellationToken.None);

        result.Should().BeNull();
        stream.Position.Should().BeLessThanOrEqualTo(Cap + PluginApiOptions.ReadChunkBytes);
    }

    /// <summary>A body that declares more than it sends is read as what really arrives.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredLengthLargerThanSent_IsReadAsWhatArrives()
    {
        var result = await PluginBodyReader.ReadAsync(Input(new MemoryStream(new byte[10]), Cap), Cap, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.Length.Should().Be(10);
    }

    /// <summary>A body that declares zero but sends bytes is read whole: the declared length is never trusted.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredZeroWithABody_IsReadWhole()
    {
        var result = await PluginBodyReader.ReadAsync(Input(new MemoryStream(new byte[300]), 0), Cap, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.Length.Should().Be(300);
    }

    /// <summary>A negative declared length does not break the buffer sizing and the body is still read.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ANegativeDeclaredLength_IsReadAsWhatArrives()
    {
        var result = await PluginBodyReader.ReadAsync(Input(new MemoryStream(new byte[42]), -5), Cap, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.Length.Should().Be(42);
    }
}
