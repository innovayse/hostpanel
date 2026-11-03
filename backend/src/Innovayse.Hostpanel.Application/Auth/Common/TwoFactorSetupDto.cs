using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// Response DTO for the 2FA setup endpoint.
/// Contains the TOTP secret and a QR code URI for the user's authenticator app.
/// </summary>
/// <param name="Secret">The Base32-encoded TOTP secret: shown to the user as a manual entry backup code.</param>
/// <param name="QrCodeUri">The <c>otpauth://totp/</c> URI to be encoded as a QR code by the frontend.</param>
public sealed record TwoFactorSetupDto(string Secret, [property: SecretMember] string QrCodeUri)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
