namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

/// <summary>
/// SSO-mode two-factor authentication: proxies to the SSO's own, already-working TOTP
/// endpoints using the caller's own bearer token.
///
/// <para>
/// Not the service key: two-factor is a self-service action on the signed-in person's own
/// account, and the SSO's TOTP endpoints authenticate exactly the way hostpanel's own
/// <c>[Authorize]</c> endpoints already do in this mode — a bearer token identifying the
/// person, not a credential identifying the platform. The existing current-request
/// abstraction does not expose the raw header, so it is read directly off
/// <see cref="HttpContext"/> here, which Infrastructure is allowed to do.
/// </para>
/// </summary>
/// <param name="client">The typed client that calls the SSO's TOTP endpoints.</param>
/// <param name="httpContextAccessor">Source of the current request's own bearer token.</param>
public sealed class SsoTwoFactorService(
    SsoTwoFactorClient client,
    IHttpContextAccessor httpContextAccessor) : ITwoFactorService
{
    /// <inheritdoc/>
    public async Task<TwoFactorSetupDto?> StartSetupAsync(string userId, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null) return null;

        var enrolment = await client.EnableAsync(token, ct);
        return new TwoFactorSetupDto(enrolment.Secret, enrolment.QrUri);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The SSO's verify response carries backup codes on success — incidental information
    /// hostpanel has nowhere to put today, since <see cref="TwoFactorSetupDto"/> and this
    /// method's <see cref="bool"/> result predate this proxy and are kept unchanged. They are
    /// discarded here, not lost: two-factor never worked in this mode before this fix, so no
    /// caller has ever seen a backup code from it. Surfacing them is follow-up work, not a
    /// regression.
    /// </remarks>
    public async Task<bool> EnableAsync(string userId, string code, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null) return false;

        var result = await client.VerifyAsync(token, code, ct);
        return result is not null;
    }

    /// <inheritdoc/>
    public async Task<bool> DisableAsync(string userId, string code, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null) return false;

        return await client.DisableAsync(token, code, ct);
    }

    /// <summary>
    /// The current request's own bearer token, or null when it is not bearer-authenticated
    /// (for example, an admin signed in through the cookie session instead) or there is no
    /// current request at all.
    /// </summary>
    private string? BearerToken()
    {
        StringValues header = httpContextAccessor.HttpContext?.Request.Headers.Authorization ?? StringValues.Empty;
        var value = header.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? value["Bearer ".Length..]
            : null;
    }
}
