namespace Innovayse.Application.Auth.Commands.Setup2Fa;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Handles <see cref="Setup2FaCommand"/>.
/// Generates a fresh TOTP secret, persists it, and returns the secret and QR code URI.
/// 2FA remains disabled until the user verifies with a valid code via Enable2FaCommand.
/// </summary>
/// <param name="userService">Abstraction over Identity user management.</param>
public sealed class Setup2FaHandler(IUserService userService)
{
    /// <summary>
    /// Generates the TOTP secret and builds the <c>otpauth://totp/…</c> URI.
    /// </summary>
    /// <param name="cmd">The command carrying the user ID and email.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A DTO containing the Base32 secret and the QR code URI.</returns>
    public async Task<TwoFactorSetupDto> HandleAsync(Setup2FaCommand cmd, CancellationToken ct)
    {
        var secret = await userService.GenerateTwoFactorSecretAsync(cmd.UserId, ct);
        var qrCodeUri = $"otpauth://totp/Innovayse:{Uri.EscapeDataString(cmd.Email)}?secret={secret}&issuer=Innovayse&digits=6&period=30";
        return new TwoFactorSetupDto(secret, qrCodeUri);
    }
}
