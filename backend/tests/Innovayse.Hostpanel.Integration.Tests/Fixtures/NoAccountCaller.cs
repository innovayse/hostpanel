using Microsoft.AspNetCore.Mvc.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A signed-in caller the identity plugin vouches for whose external account has no client row, so they act for no
/// account: the host derived for them and the client that talks to it. Disposing it disposes both.
/// </summary>
/// <param name="Host">The derived host whose identity plugin vouches for the caller.</param>
/// <param name="Http">The client signed in as the caller.</param>
public sealed record NoAccountCaller(WebApplicationFactory<Program> Host, HttpClient Http) : IDisposable
{
    /// <summary>Disposes the client, then the host it talks to.</summary>
    public void Dispose()
    {
        Http.Dispose();
        Host.Dispose();
    }
}
