namespace Innovayse.Hostpanel.Infrastructure.Resilience;

/// <summary>
/// The outer <c>HttpClient.Timeout</c> each outbound client is registered with. These are
/// backstops only: the resilience pipeline on the same client decides first, and the value here
/// catches a pipeline that was configured wrong. They are code, not configuration, because a
/// backstop an operator can lower below the pipeline's budget defeats its purpose.
/// </summary>
public static class HttpBackstopTimeouts
{
    /// <summary>cPanel/WHM provisioning client.</summary>
    public static readonly TimeSpan CPanel = TimeSpan.FromSeconds(60);

    /// <summary>Inecobank gateway client.</summary>
    public static readonly TimeSpan Inecobank = TimeSpan.FromSeconds(60);

    /// <summary>Telegram enquiry notifier; short because it sits between an enquiry and the visitor's answer.</summary>
    public static readonly TimeSpan Telegram = TimeSpan.FromSeconds(10);

    /// <summary>CWP control-panel clients, typed and named.</summary>
    public static readonly TimeSpan Cwp = TimeSpan.FromSeconds(15);

    /// <summary>CWP7 control-panel clients, typed and named.</summary>
    public static readonly TimeSpan Cwp7 = TimeSpan.FromSeconds(120);

    /// <summary>Plesk XML API client, resolved by name per server.</summary>
    public static readonly TimeSpan Plesk = TimeSpan.FromSeconds(120);

    /// <summary>The migration pull client.</summary>
    public static readonly TimeSpan Migration = TimeSpan.FromMinutes(5);

    /// <summary>Domain registrar clients (Name.am and Namecheap).</summary>
    public static readonly TimeSpan Registrar = TimeSpan.FromSeconds(30);

    /// <summary>Per-server WHM client used for disk-usage reporting.</summary>
    public static readonly TimeSpan Whm = TimeSpan.FromSeconds(30);
}
