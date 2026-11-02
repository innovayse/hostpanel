namespace Innovayse.Hostpanel.Infrastructure.Resilience;

/// <summary>
/// The names of the named <c>HttpClient</c> registrations that more than one place has to spell
/// identically. <see cref="System.Net.Http.IHttpClientFactory"/> answers an unknown name with a
/// plain default client rather than an error, so a mismatch is silent and drops the profile.
/// </summary>
public static class HttpClientNames
{
    /// <summary>The CWP7 control-panel client the provisioning factory resolves per server.</summary>
    public const string Cwp7 = "Cwp7";

    /// <summary>The Plesk XML API client the provisioning factory and the connection tester resolve per server.</summary>
    public const string Plesk = "Plesk";

    /// <summary>The client that pulls records out of a foreign install.</summary>
    public const string Migration = "migration";
}
