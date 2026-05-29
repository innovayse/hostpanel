namespace Innovayse.Infrastructure.Common;

using System.Security.Claims;
using Innovayse.Application.Common;
using Innovayse.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Provides current HTTP request context (admin identity and IP) for Application layer handlers.
/// Resolves identity from <see cref="ClaimsPrincipal"/> and remote IP from <see cref="IHttpContextAccessor"/>.
/// Admin name is resolved by looking up the <see cref="AppUser"/> via <see cref="UserManager{TUser}"/>,
/// because the JWT does not carry a display name claim.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
/// <param name="userManager">Identity user manager used to resolve the admin display name.</param>
public sealed class HttpCurrentRequestContext(
    IHttpContextAccessor httpContextAccessor,
    UserManager<AppUser> userManager) : ICurrentRequestContext
{
    /// <inheritdoc/>
    public string? AdminId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc/>
    public string? AdminName
    {
        get
        {
            var userId = AdminId;
            if (userId is null)
            {
                return null;
            }

            // UserManager.FindByIdAsync is synchronous-safe here because we run it inline.
            // The result is cached per-request via scoped lifetime.
            var user = userManager.FindByIdAsync(userId).GetAwaiter().GetResult();
            return user is null ? null : $"{user.FirstName} {user.LastName}".Trim();
        }
    }

    /// <inheritdoc/>
    public string? AdminEmail =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    /// <inheritdoc/>
    public string? IpAddress =>
        httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
