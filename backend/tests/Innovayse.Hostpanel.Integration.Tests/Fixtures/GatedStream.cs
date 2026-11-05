using System.Runtime.CompilerServices;
using Hostpanel.Sdk.Plugins.Endpoints;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A plugin event source shared by the streaming tests: one event, a wait for a gate, a last event.</summary>
public static class GatedStream
{
    /// <summary>A stream that sends one event, waits for a gate to open, then sends a last event.</summary>
    /// <param name="gate">Completed by the test to let the stream go on; complete already, the stream does not wait.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <returns>The events: <c>first</c>, then <c>last</c> once the gate is open.</returns>
    public static async IAsyncEnumerable<PluginEvent> Open(Task gate, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new PluginEvent("first");
        await gate.WaitAsync(ct);
        yield return new PluginEvent("last");
    }
}
