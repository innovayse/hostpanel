namespace Innovayse.Application.Auth.DTOs;

/// <summary>
/// Response DTO for the 2FA setup endpoint.
/// Contains the TOTP secret and a QR code URI for the user's authenticator app.
/// </summary>
/// <param name="Secret">The Base32-encoded TOTP secret — shown to the user as a manual entry backup code.</param>
/// <param name="QrCodeUri">The <c>otpauth://totp/…</c> URI to be encoded as a QR code by the frontend.</param>
public record TwoFactorSetupDto(string Secret, string QrCodeUri);
