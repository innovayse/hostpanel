using System.Security.Claims;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A stub identity plugin: answers from a table keyed by the principal subject, and with
/// <see langword="null"/> for a subject the table does not list. Stands in for a plugin assembly, which a
/// test must not load.
/// </summary>
public sealed class StubClientResolver : IClientResolver
{
    /// <summary>What the stub grants, keyed by subject.</summary>
    private readonly Dictionary<string, ClientAccess> _grants = [];

    /// <summary>How many times the host asked this plugin who a principal is.</summary>
    private int _calls;

    /// <summary>Gets how many times the host asked this plugin who a principal is; a request must ask at most once.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Makes the stub grant <paramref name="access"/> to the principal whose subject is <paramref name="subject"/>.</summary>
    /// <param name="subject">The subject the grant is for.</param>
    /// <param name="access">What the plugin grants it.</param>
    public void Grant(string subject, ClientAccess access) => _grants[subject] = access;

    /// <inheritdoc/>
    public Task<ClientAccess?> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

        return Task.FromResult(subject is not null && _grants.TryGetValue(subject, out var access) ? access : null);
    }
}
