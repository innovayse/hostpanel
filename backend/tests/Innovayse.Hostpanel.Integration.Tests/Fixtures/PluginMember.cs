using Microsoft.AspNetCore.Mvc.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A signed-in user an enabled identity plugin vouches for as a member of an external account, with the host
/// whose plugin says so. Disposing releases the host and the HTTP client.
/// </summary>
/// <param name="Host">The test host whose identity plugin is the stub.</param>
/// <param name="Client">The HTTP client carrying the member's bearer token, talking to <paramref name="Host"/>.</param>
/// <param name="OwnClientId">The id of the client record the member registered with, which is never the account they act for.</param>
/// <param name="OwnEmail">The address that client record was registered with.</param>
public sealed record PluginMember(WebApplicationFactory<Program> Host, HttpClient Client, int OwnClientId, string OwnEmail) : IDisposable
{
    /// <summary>Releases the HTTP client and the host.</summary>
    public void Dispose()
    {
        Client.Dispose();
        Host.Dispose();
    }
}
