namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// Counts how many times the plugin switch and settings were read. A plain singleton with a constructor the host can build
/// itself, so the handler code the host generates can create it without looking it up at run time.
/// </summary>
public sealed class PluginPrepareCounter
{
    /// <summary>The number of reads so far.</summary>
    private int _count;

    /// <summary>Gets the number of reads so far.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Records one read.</summary>
    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }
}
