using System.Text;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Auth;

/// <summary>Wire shape of the sign-on provider answer that starts a TOTP enrolment.</summary>
/// <param name="Secret">The shared secret in base32.</param>
/// <param name="QrUri">The <c>otpauth://</c> URI an authenticator app scans.</param>
internal sealed record SsoTotpEnrolmentWire(
    [property: JsonPropertyName("secret")] string Secret,
    [property: JsonPropertyName("qrUri")] string QrUri)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
