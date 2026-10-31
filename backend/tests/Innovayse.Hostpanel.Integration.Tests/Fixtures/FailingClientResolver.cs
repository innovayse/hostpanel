using System.Security.Claims;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A stub identity plugin that never answers: every call throws the failure a test gave it (a fault, a
/// <see cref="TimeoutException"/> or the cancellation an HTTP client raises when its own timeout fires).
/// </summary>
/// <param name="failure">What the plugin throws on every call.</param>
public sealed class FailingClientResolver(Exception failure) : IClientResolver
{
    /// <summary>How many times the host asked this plugin who a principal is.</summary>
    private int _calls;

    /// <summary>Gets how many times the host asked this plugin who a principal is.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc/>
    public Task<ClientAccess?> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);

        throw failure;
    }
}
