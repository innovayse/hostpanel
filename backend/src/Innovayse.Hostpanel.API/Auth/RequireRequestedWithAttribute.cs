using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Innovayse.Hostpanel.API.Auth;

/// <summary>
/// Refuses a request that changes state and carries neither <c>X-Requested-With: XMLHttpRequest</c> nor a bearer
/// token (403, <c>CSRF_HEADER_MISSING</c>), whatever it authenticated with. For the routes that hand the request
/// to a plugin: <see cref="LocalSessionCsrfMiddleware"/> refuses a cookie-authenticated request in local mode and the
/// external sign-on package does the same in its mode, but code the host does not own should not depend on which
/// credential the request happened to ride on, so these routes state the rule themselves, from the same
/// <see cref="CsrfRule"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireRequestedWithAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>Refuses the request unless it is safe, carries the header, or is a bearer call.</summary>
    /// <param name="context">The request being authorized.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.HttpContext.Request;
        if (CsrfRule.ChangesState(request) && !CsrfRule.HasBearer(request) && !CsrfRule.HasRequestedWith(request))
        {
            context.Result = ErrorBody.ToActionResult(CsrfRule.Refusal(context.HttpContext.RequestServices));
        }
    }
}
