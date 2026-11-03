namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// One call's record of whether the host's store refused a write over its quota. Built per call beside the store and
/// read by the call runner: a plugin cannot set it, so a plugin cannot make the host answer with the quota code, and a
/// plugin that catches the store's refusal still never gets the write.
/// </summary>
public sealed class PluginStoreQuotaSignal
{
    /// <summary>Gets a value indicating whether the store refused a write of this call over the scope's quota.</summary>
    public bool Raised { get; private set; }

    /// <summary>Records that the store refused a write over the quota. Called only by the store.</summary>
    public void Raise()
    {
        Raised = true;
    }
}
