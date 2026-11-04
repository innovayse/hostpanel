using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.NameAm;

/// <summary>The body of Name.am's <c>POST /auth/login</c>.</summary>
/// <param name="Email">The reseller account e-mail.</param>
/// <param name="Password">The reseller account password.</param>
/// <param name="Token">The second-factor token field the API expects; always empty, as the account has none.</param>
internal sealed record NameAmLoginWire(string Email, string Password, string Token)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
