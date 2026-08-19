namespace Innovayse.Infrastructure.Common;

using System.Security.Claims;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Provides current HTTP request context (admin identity and IP) for Application layer handlers.
/// Resolves identity from <see cref="ClaimsPrincipal"/> and remote IP from <see cref="IHttpContextAccessor"/>.
/// </summary>
/// <remarks>
/// <para>
/// The display name is looked up rather than read off the token, because the JWT does not
/// carry one. It goes through <see cref="IIdentityProvider"/>, which answers from this
/// product's own users or from the SSO depending on the deployment — this used to take
/// Identity's <c>UserManager</c> directly, which a deployment whose people live in the
/// SSO does not register, so it could not be constructed there at all. Since every admin
/// write records who did it, that was every admin write failing.
/// </para>
/// <para>
/// If the provider cannot answer, the read throws and takes the admin's action with it,
/// rather than recording the action against nobody. Deliberate: a failed write can be
/// retried, whereas an audit row that has lost who performed it is wrong for good, and
/// the whole point of the row is accountability. It does not apply to a caller the
/// provider simply does not know — that answers null and is recorded as null.
/// </para>
/// </remarks>
/// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
/// <param name="identity">Where this deployment's people are read from.</param>
public sealed class HttpCurrentRequestContext(
    IHttpContextAccessor httpContextAccessor,
    IIdentityProvider identity) : ICurrentRequestContext
{
    private bool _nameResolved;
    private string? _name;

    /// <summary>
    /// Both claim names, because the two modes deliver them differently. SSO mode sets
    /// MapInboundClaims = false, so "sub" and "email" survive as themselves; local mode
    /// leaves the default mapping on, which renames them. Reading only the mapped name
    /// found nothing under the SSO, and since these are what an audit row records, every
    /// admin action was being logged with no idea who performed it.
    /// </summary>
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc/>
    public string? AdminId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");

    /// <inheritdoc/>
    public string? AdminName
    {
        get
        {
            // Once per request, not once per read. A single admin action records the name
            // several times over, and where the SSO owns the people each of those reads is
            // an HTTP call rather than a query against a context that had already loaded
            // the row.
            if (_nameResolved)
            {
                return _name;
            }

            _nameResolved = true;

            var subject = AdminId;
            if (subject is null)
            {
                return _name = null;
            }

            var account = identity.FindBySubjectAsync(subject, CancellationToken.None)
                .GetAwaiter().GetResult();

            return _name = account is null
                ? null
                : $"{account.FirstName} {account.LastName}".Trim();
        }
    }

    /// <inheritdoc/>
    public string? AdminEmail =>
        User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");

    /// <inheritdoc/>
    public string? IpAddress =>
        httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
