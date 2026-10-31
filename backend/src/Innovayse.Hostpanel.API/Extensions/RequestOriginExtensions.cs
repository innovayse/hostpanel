using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.API.Extensions;

/// <summary>Reads where a request came from, to keep with a client's agreement as proof.</summary>
public static class RequestOriginExtensions
{
    /// <summary>The address and browser of the request, as the server saw them.</summary>
    /// <param name="request">The request.</param>
    /// <returns>
    /// The origin. The address is the connection's own (the forwarded-header middleware has already replaced a
    /// proxy's address with the client's), never a header the caller could set to anything.
    /// </returns>
    public static CardConsentOrigin ConsentOrigin(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = request.Headers.UserAgent.ToString();
        return new CardConsentOrigin(request.HttpContext.Connection.RemoteIpAddress?.ToString(), agent.Length == 0 ? null : agent);
    }
}
