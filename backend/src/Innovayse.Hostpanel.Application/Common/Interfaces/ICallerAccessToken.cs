namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The bearer token the current caller presented, for an adapter that has to act as that person
/// against an outside service rather than with this product's own credential.
/// </summary>
/// <remarks>
/// Implemented in the API, where the request is; nothing below it reads the request itself.
/// </remarks>
public interface ICallerAccessToken
{
    /// <summary>Reads the caller's bearer token.</summary>
    /// <returns>
    /// The token, or <c>null</c> when the caller is not bearer-authenticated (for example a cookie
    /// session) or there is no current request.
    /// </returns>
    public string? GetBearerToken();
}
