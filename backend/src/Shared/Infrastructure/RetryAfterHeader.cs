using System.Globalization;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Stamps <c>Retry-After</c> on a 503, which the API contract requires: a dependency could not
/// answer, and the caller needs to know when asking again is worth it. Every error surface that can
/// answer 503 calls this, so the header cannot be missing on one of them.
/// </summary>
public static class RetryAfterHeader
{
    /// <summary>Sets <c>Retry-After</c> from <see cref="ServiceUnavailableOptions"/> when <paramref name="status"/> is 503.</summary>
    /// <param name="context">The request whose response is being written.</param>
    /// <param name="status">The status the response will carry.</param>
    public static void ApplyFor(HttpContext context, int status)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (status != StatusCodes.Status503ServiceUnavailable)
        {
            return;
        }

        var options = context.RequestServices.GetRequiredService<IOptions<ServiceUnavailableOptions>>().Value;
        context.Response.Headers.RetryAfter = options.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
    }
}
