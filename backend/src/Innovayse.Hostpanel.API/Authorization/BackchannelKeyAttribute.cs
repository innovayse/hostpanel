using System.Security.Cryptography;
using System.Text;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// Admits only the platform portal's server-to-server calls: the request must carry the shared
/// key in <c>X-Logout-Key</c>. The internal-endpoint mechanism of this product.
/// </summary>
/// <remarks>
/// A filter rather than a check inside the action so a route added to an internal controller
/// cannot leave the check out. An unset key refuses everything: treating "not configured" as
/// "no check" would turn a missing setting into an open door.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BackchannelKeyAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>The header the key travels in.</summary>
    public const string HeaderName = "X-Logout-Key";

    /// <summary>Refuses the request unless the header carries the configured key.</summary>
    /// <param name="context">The request being authorized.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.HttpContext.RequestServices;
        var expected = services.GetRequiredService<IOptions<BackchannelLogoutOptions>>().Value.Key;
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        // Hashed to a fixed length first, then compared in fixed time: a plain comparison
        // returns at the first differing byte and leaks the key one character at a time.
        var matches = !string.IsNullOrEmpty(expected)
            && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)));

        if (matches)
        {
            return;
        }

        context.Result = ErrorBody.ToActionResult(ErrorBody.ForCode(services, HostpanelErrorCodes.BACKCHANNEL_LOGOUT_UNAUTHORIZED));
    }
}
