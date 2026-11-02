using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.API.Auth;

/// <summary>
/// The one definition of the cross-site request forgery rule: a request that changes state must carry the header a
/// cross-site page cannot set, unless it is authenticated by a bearer token, which a cross-site page cannot attach
/// either. <see cref="LocalSessionCsrfMiddleware"/> applies it to every request that presents the session cookie;
/// <see cref="RequireRequestedWithAttribute"/> applies it to the routes that hand a request to code the host does not own.
/// </summary>
public static class CsrfRule
{
    /// <summary>
    /// Header a cross-site page cannot set without a CORS preflight this API does not grant.
    /// The admin SPA's fetch wrapper sends it on every request.
    /// </summary>
    public const string RequestedWithHeader = "X-Requested-With";

    /// <summary>Value the admin SPA sends in <see cref="RequestedWithHeader"/>.</summary>
    public const string RequestedWithValue = "XMLHttpRequest";

    /// <summary>Resource key of the sentence a refused caller reads.</summary>
    private const string DetailKey = "CsrfHeaderMissingDetail";

    /// <summary>Title of the refusal body.</summary>
    private const string Title = "Forbidden";

    /// <summary>Methods that cannot change state, and are therefore never refused.</summary>
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace };

    /// <summary>Whether the request's method can change state.</summary>
    /// <param name="request">The incoming request.</param>
    /// <returns><see langword="true"/> for every method that is not GET, HEAD, OPTIONS or TRACE.</returns>
    public static bool ChangesState(HttpRequest request)
    {
        return !SafeMethods.Contains(request.Method);
    }

    /// <summary>Whether the request is authenticated by a real bearer token.</summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>
    /// <see langword="true"/> when the <c>Authorization</c> header carries a non-empty bearer token. Any other value (a Basic
    /// pair, an empty or malformed scheme) authenticates nothing here, so it does not exempt a request.
    /// </returns>
    public static bool HasBearer(HttpRequest request)
    {
        return BearerHeader.TryRead(request, out _);
    }

    /// <summary>Whether the request carries the expected header.</summary>
    /// <param name="request">The incoming request.</param>
    /// <returns><see langword="true"/> when the header is present with the expected value.</returns>
    public static bool HasRequestedWith(HttpRequest request)
    {
        return request.Headers.TryGetValue(RequestedWithHeader, out var values)
            && values.Any(value => string.Equals(value, RequestedWithValue, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The refusal body, with the sentence in the caller's language.</summary>
    /// <param name="services">The request's services, which hold the localizer.</param>
    /// <returns>The standard error body with <see cref="SharedErrorCodes.CsrfHeaderMissing"/> (403).</returns>
    public static ErrorResponse Refusal(IServiceProvider services)
    {
        var localizer = services.GetRequiredService<IStringLocalizer<ValidationMessages>>();
        return new ErrorResponse(SharedErrorCodes.CsrfHeaderMissing, Title, StatusCodes.Status403Forbidden, localizer[DetailKey]);
    }
}
