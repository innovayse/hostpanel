namespace Innovayse.Infrastructure.Auth;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The SSO's own TOTP endpoints, as this product calls them on behalf of the signed-in
/// caller.
///
/// <para>
/// Every call carries the caller's own bearer token, never the service key: two-factor is a
/// self-service action on the signed-in person's own account, not something this product
/// performs on its own authority, and the SSO's TOTP endpoints are shaped to match — they
/// authenticate the same way hostpanel's own <c>[Authorize]</c> endpoints already do in this
/// mode. Passed as a parameter on every call rather than set as a default header on the
/// client, because unlike <see cref="SsoServiceClient"/> this client has no single fixed
/// credential to attach at registration time.
/// </para>
/// </summary>
public sealed class SsoTwoFactorClient(HttpClient http)
{
    /// <summary>Starts enrolment and returns the secret and QR URI to show the caller.</summary>
    /// <param name="bearerToken">The caller's own access token.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SsoTotpEnrolment> EnableAsync(string bearerToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/account/totp/enable");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TotpEnrolmentResponse>(ct)
            ?? throw new InvalidOperationException("SSO returned an empty TOTP enrolment response.");
        return new SsoTotpEnrolment(body.Secret, body.QrUri);
    }

    /// <summary>
    /// Submits a TOTP code to complete enrolment (or, on the SSO's own re-auth challenge
    /// path, to satisfy it — hostpanel only ever calls this to complete enrolment).
    /// </summary>
    /// <param name="bearerToken">The caller's own access token.</param>
    /// <param name="totpCode">The six digits the authenticator app currently shows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The verification result, or null when the SSO rejected the code as not matching.
    /// </returns>
    public async Task<SsoTotpVerification?> VerifyAsync(string bearerToken, string totpCode, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/account/totp/verify")
        {
            Content = JsonContent.Create(new TotpCodeRequest(totpCode)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TotpVerificationResponse>(ct);
        return new SsoTotpVerification(body?.BackupCodes);
    }

    /// <summary>Switches two-factor off, once <paramref name="totpCode"/> proves the caller still holds it.</summary>
    /// <param name="bearerToken">The caller's own access token.</param>
    /// <param name="totpCode">A current code from the authenticator app.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when it was switched off; false when the SSO rejected the code.</returns>
    public async Task<bool> DisableAsync(string bearerToken, string totpCode, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/account/totp/disable")
        {
            Content = JsonContent.Create(new TotpCodeRequest(totpCode)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>The secret and enrolment URI the SSO issued.</summary>
    public sealed record SsoTotpEnrolment(string Secret, string QrUri);

    /// <summary>
    /// The result of submitting a code. <see cref="BackupCodes"/> is present when this
    /// completed an enrolment and null when it satisfied a re-auth challenge instead.
    /// </summary>
    public sealed record SsoTotpVerification(IReadOnlyList<string>? BackupCodes);

    private sealed record TotpCodeRequest(
        [property: JsonPropertyName("totpCode")] string TotpCode);

    private sealed record TotpEnrolmentResponse(
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("qrUri")] string QrUri);

    private sealed record TotpVerificationResponse(
        [property: JsonPropertyName("backupCodes")] IReadOnlyList<string>? BackupCodes);
}
