namespace Innovayse.Hostpanel.Infrastructure.Resilience;

/// <summary>
/// The names of the named <c>HttpClient</c> registrations that more than one place has to spell
/// identically. <see cref="System.Net.Http.IHttpClientFactory"/> answers an unknown name with a
/// plain default client rather than an error, so a mismatch is silent and drops the profile.
/// </summary>
public static class HttpClientNames
{
    /// <summary>The WHM API client the provisioning factory and the connection tester resolve per cPanel server.</summary>
    public const string CPanelWhm = "CPanelWhm";

    /// <summary>The client <c>PanelTransport</c> hands a plugin; the same name every place that builds it must use.</summary>
    public const string PanelTransport = "PanelTransport";

    /// <summary>The client that pulls records out of a foreign install.</summary>
    public const string Migration = "migration";
}
