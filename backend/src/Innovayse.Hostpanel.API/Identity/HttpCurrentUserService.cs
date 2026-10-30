using System.Security.Claims;
using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Innovayse.Hostpanel.API.Identity;

/// <summary>
/// The caller of the current HTTP request (identity and IP), for Application-layer handlers.
/// Resolves identity from <see cref="ClaimsPrincipal"/> and remote IP from <see cref="IHttpContextAccessor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Lives in API because it is a request-scoped adapter over <see cref="HttpContext"/>. It
/// implements <see cref="ICurrentUserService"/>, the one port every handler takes for identity.
/// </para>
/// <para>
/// This is the only place in the product that reads a <see cref="ClaimsPrincipal"/>. Controllers
/// do not, and neither does a base controller: a lookup on a base class can only be reached by
/// inheriting from it, which handlers cannot do, so the identity would have to be passed down on
/// the message, which is the thing this type exists to prevent.
/// </para>
/// <para>
/// The display name is looked up rather than read off the token, because the JWT does not
/// carry one. It goes through <see cref="IIdentityProvider"/>, which answers from this
/// product's own users or from the external sign-on provider depending on the deployment. It does not take
/// Identity's <c>UserManager</c> directly: a deployment whose people live in the external sign-on provider does not
/// register it, and every admin write records who did it.
/// </para>
/// <para>
/// If the provider cannot answer, the lookup throws and takes the admin's action with it,
/// rather than recording the action against nobody. Deliberate: a failed write can be
/// retried, whereas an audit row that has lost who performed it is wrong for good, and
/// the whole point of the row is accountability. It does not apply to a caller the
/// provider simply does not know: that answers null and is recorded as null.
/// </para>
/// </remarks>
public sealed class HttpCurrentUserService : ICurrentUserService
{
    /// <summary>Accessor for the current HTTP context.</summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Where this deployment's people are read from.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Whether <see cref="GetUserNameAsync"/> has already been resolved for this request.</summary>
    private bool _nameResolved;

    /// <summary>The cached display name once resolved, or <see langword="null"/> when there is none.</summary>
    private string? _name;

    /// <summary>Initializes a new instance of the <see cref="HttpCurrentUserService"/> class.</summary>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    /// <param name="identity">Where this deployment's people are read from.</param>
    public HttpCurrentUserService(IHttpContextAccessor httpContextAccessor, IIdentityProvider identity)
    {
        _httpContextAccessor = httpContextAccessor;
        _identity = identity;
    }

    /// <summary>
    /// Both claim names, because the two modes deliver them differently. External sign-on mode sets
    /// MapInboundClaims = false, so "sub" and "email" survive as themselves; local mode
    /// leaves the default mapping on, which renames them. Reading only the mapped name would
    /// find nothing under the external sign-on provider, and these are what an audit row records
    /// about who performed an admin action.
    /// </summary>
    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc/>
    public ClaimsPrincipal? Principal => IsAuthenticated ? User : null;

    /// <inheritdoc/>
    public string? UserId => User?.GetSubject();

    /// <inheritdoc/>
    public async Task<string?> GetUserNameAsync(CancellationToken ct)
    {
        // Once per request, not once per read. A single admin action records the name
        // several times over, and where the external sign-on provider owns the people each of those reads is
        // an HTTP call rather than a query against a context that had already loaded
        // the row.
        if (_nameResolved)
        {
            return _name;
        }

        var subject = UserId;
        if (subject is null)
        {
            _nameResolved = true;
            return _name = null;
        }

        var account = await _identity.FindBySubjectAsync(subject, ct);

        _nameResolved = true;
        return _name = account is null
            ? null
            : $"{account.FirstName} {account.LastName}".Trim();
    }

    /// <inheritdoc/>
    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");

    /// <inheritdoc/>
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    /// <inheritdoc/>
    public bool? IsEmailVerified
    {
        get
        {
            // A claim that is not there is not an answer, so it is not read as "unverified":
            // the claim only travels when the OIDC `email` scope was granted, and a token without
            // it must not make the account look unconfirmed.
            var claim = User?.FindFirstValue("email_verified");
            if (string.IsNullOrEmpty(claim))
            {
                return null;
            }

            // Present, so it is an answer either way. The claim arrives as a JSON boolean
            // rendered into a string, hence a case-insensitive text comparison rather than a
            // parse: "true", "True" and "TRUE" are the same issuer saying the same thing.
            return string.Equals(claim, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc/>
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc/>
    public string RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("User ID not found in token.");
}
