using Innovayse.Hostpanel.Application.Clients.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Keeps the answer of <see cref="ICallerClientResolver"/> for the life of one request. The request, not the
/// dependency injection scope, is the boundary: the portal policy and the permission filter run in the request's
/// scope but the bus runs a use case in a scope of its own, so a scoped field would be empty there and ask the
/// identity plugin a second time.
/// </summary>
public interface ICallerClientMemo
{
    /// <summary>Reads the answer already kept for this request.</summary>
    /// <param name="access">The kept answer, when there is one.</param>
    /// <returns><see langword="true"/> when an answer was kept and not forgotten since; <see langword="false"/> off a request.</returns>
    bool TryGet(out CallerClientAccess access);

    /// <summary>Keeps the answer for the rest of this request, replacing an earlier one. Does nothing off a request.</summary>
    /// <param name="access">The answer to keep.</param>
    void Set(CallerClientAccess access);

    /// <summary>Forgets the answer kept for this request, so the next ask reads again.</summary>
    void Forget();
}
