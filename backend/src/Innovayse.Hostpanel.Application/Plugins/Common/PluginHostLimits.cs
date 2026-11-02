namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The limits the host applies to the facts it shares with a plugin through the SDK's host API.</summary>
public static class PluginHostLimits
{
    /// <summary>The most services or invoices one list returns: the 50 most recent, as the SDK documents.</summary>
    public const int MaxListed = 50;
}
