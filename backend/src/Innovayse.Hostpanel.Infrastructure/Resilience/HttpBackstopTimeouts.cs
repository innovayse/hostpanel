namespace Innovayse.Hostpanel.Infrastructure.Resilience;

/// <summary>
/// The outer <c>HttpClient.Timeout</c> each outbound client is registered with. These are
/// backstops only: the resilience pipeline on the same client decides first, and the value here
/// catches a pipeline that was configured wrong. They are code, not configuration, because a
/// backstop an operator can lower below the pipeline's budget defeats its purpose.
/// </summary>
public static class HttpBackstopTimeouts
{
    /// <summary>The named client of every loaded plugin, keyed by its plugin id.</summary>
    public static readonly TimeSpan Plugin = TimeSpan.FromSeconds(60);

    /// <summary>Telegram enquiry notifier; short because it sits between an enquiry and the visitor's answer.</summary>
    public static readonly TimeSpan Telegram = TimeSpan.FromSeconds(10);

    /// <summary>The status page's component checks; above the profile's 12 seconds in total.</summary>
    public static readonly TimeSpan StatusProbe = TimeSpan.FromSeconds(15);

    /// <summary>Fraud scoring service; short because it sits between a checkout and its answer, and its failure falls back to local scoring.</summary>
    public static readonly TimeSpan MaxMind = TimeSpan.FromSeconds(10);

    /// <summary>CWP control-panel clients, typed and named.</summary>
    public static readonly TimeSpan Cwp = TimeSpan.FromSeconds(15);

    /// <summary>WHM API client, resolved by name per cPanel server.</summary>
    public static readonly TimeSpan CPanelWhm = TimeSpan.FromSeconds(120);

    /// <summary>
    /// The client the host hands a panel, virtual server or registrar plugin through <c>IPanelTransport</c>. It has no
    /// resilience pipeline, so this is its only timeout: as long as the slowest panel budget (account creation), so it never
    /// cuts a call short that the panel's own profile would allow.
    /// </summary>
    public static readonly TimeSpan PanelTransport = TimeSpan.FromSeconds(120);

    /// <summary>The migration pull client.</summary>
    public static readonly TimeSpan Migration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The exchange rate banks, typed clients. Above the 15 seconds the resilience profile allows a call, and the only bound on a
    /// body that stalls after its headers: the pipeline ends when the headers arrive, the client buffers the body after that.
    /// </summary>
    public static readonly TimeSpan ExchangeRate = TimeSpan.FromSeconds(20);

    /// <summary>The VAT number registers (the EU service and the UK authority); above the profile's 25 seconds in total.</summary>
    public static readonly TimeSpan VatRegister = TimeSpan.FromSeconds(30);
}
