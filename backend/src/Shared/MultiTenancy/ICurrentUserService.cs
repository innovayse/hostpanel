using System.Security.Claims;

namespace Innovayse.Hostpanel.Shared.MultiTenancy;

/// <summary>
/// The caller, as far as a handler is concerned. Keeps <c>HttpContext</c> out of the Application layer:
/// identity comes from the token through this port, never from a command's body.
/// </summary>
/// <remarks>
/// Identifiers are strings (the sign-on provider <c>sub</c>, or the local Identity user id), not
/// <c>Guid</c>s, and there is no tenant: hostpanel bills clients.
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>
    /// The authenticated caller with its claims, for the one consumer that must hand it on whole (an identity
    /// plugin decides from the principal's claims); null when the request carries no authenticated caller.
    /// </summary>
    public ClaimsPrincipal? Principal { get; }

    /// <summary>The caller's subject id, or null when the request carries no authenticated caller.</summary>
    public string? UserId { get; }

    /// <summary>The caller's display name, resolved at most once per request; null when unknown.</summary>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The display name, or null when there is no caller or the caller is unknown.</returns>
    public Task<string?> GetUserNameAsync(CancellationToken ct);

    /// <summary>The <c>email</c> claim, or null when the token omitted it.</summary>
    public string? Email { get; }

    /// <summary>
    /// Whether the issuer vouched for the email address: <c>true</c> or <c>false</c> when the token
    /// says so, null when it carries no <c>email_verified</c> claim at all (which is not an answer).
    /// </summary>
    public bool? IsEmailVerified { get; }

    /// <summary>The caller's address as the forwarded-headers middleware resolved it; null off a request.</summary>
    public string? IpAddress { get; }

    /// <summary>Whether a principal is present at all.</summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// The caller's subject id. Throws <see cref="UnauthorizedAccessException"/> (answered 401) when
    /// there is none; a handler behind an authorized action never sees that.
    /// </summary>
    /// <returns>The subject id.</returns>
    public string RequireUserId();
}
