namespace Innovayse.Application.Common;

/// <summary>
/// The caller behind the current HTTP request, as far as the Application layer is concerned.
/// Implemented in Infrastructure via <c>IHttpContextAccessor</c>, which is the only place a
/// <c>ClaimsPrincipal</c> is read.
/// </summary>
/// <remarks>
/// <para>
/// This is how a handler learns who is calling. Identity is deliberately not a field on any
/// command or query: a caller who can name the subject is a caller who can act as somebody
/// else, and an ownership check made at the endpoint travels separately from the message the
/// moment anything but that endpoint dispatches it.
/// </para>
/// <para>
/// The members are named for the caller, not for a role. Admin writes record
/// <see cref="UserId"/>, <see cref="UserName"/> and <see cref="UserEmail"/> on their audit
/// rows; client-portal handlers read <see cref="RequireUserId"/> to scope a query to the
/// account it belongs to. Both are "who is calling", so there is one abstraction for it.
/// </para>
/// </remarks>
public interface ICurrentRequestContext
{
    /// <summary>Gets the subject (Identity user ID) of the caller, or <see langword="null"/> if unauthenticated.</summary>
    string? UserId { get; }

    /// <summary>Gets the display name of the caller, or <see langword="null"/> if unavailable.</summary>
    string? UserName { get; }

    /// <summary>Gets the email of the caller, or <see langword="null"/> if unavailable.</summary>
    string? UserEmail { get; }

    /// <summary>
    /// Gets a value indicating whether the caller's email address has been confirmed, according
    /// to the credential they presented.
    /// </summary>
    /// <remarks>
    /// Read from the <c>email_verified</c> claim. A credential that does not carry the claim at
    /// all answers <see langword="false"/>: an unverified address must never be mistaken for a
    /// verified one because the issuer stayed silent about it.
    /// </remarks>
    bool IsEmailVerified { get; }

    /// <summary>Gets the remote IP address of the current request, or <see langword="null"/> if unavailable.</summary>
    string? IpAddress { get; }

    /// <summary>
    /// Returns the caller's subject, refusing the request when there is not one.
    /// </summary>
    /// <returns>The caller's Identity user ID.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the request carries no usable subject. A handler that scopes data to the
    /// caller has no safe answer without one, so this refuses rather than falling back to a
    /// value the caller could have chosen.
    /// </exception>
    string RequireUserId();
}
