using Innovayse.Hostpanel.Application.Clients.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Keeps the answer of <see cref="ICallerClientResolver"/> for the life of one request. The request, not the
/// dependency injection scope, is the boundary: the portal policy and the permission filter run in the request's
/// scope but the bus runs a use case in a scope of its own, so a scoped field would be empty there and ask the
/// identity plugin a second time. Every answer is stored with the subject it was resolved for and is only handed
/// back to that subject, so it can never cross principals.
/// </summary>
public interface ICallerClientMemo
{
    /// <summary>Reads the answer already kept for this request and this subject.</summary>
    /// <param name="userId">The subject asking now.</param>
    /// <param name="access">The kept answer, when there is one.</param>
    /// <returns>
    /// <see langword="true"/> when an answer was kept for exactly this subject and not forgotten since;
    /// <see langword="false"/> otherwise, and always off a request.
    /// </returns>
    bool TryGet(string userId, out CallerClientAccess access);

    /// <summary>Keeps the answer for the rest of this request, replacing an earlier one. Does nothing off a request.</summary>
    /// <param name="userId">The subject the answer was resolved for.</param>
    /// <param name="access">The answer to keep.</param>
    void Set(string userId, CallerClientAccess access);

    /// <summary>Forgets the answer kept for this request, so the next ask reads again.</summary>
    void Forget();
}
