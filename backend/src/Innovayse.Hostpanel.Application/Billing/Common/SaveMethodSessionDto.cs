using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The session a payer enters a payment method in to have it stored at a gateway. Which half is set depends on
/// <paramref name="Mode"/>: an embedded plugin's own front end part mounts its card field with the token, a redirect
/// plugin sends the payer to the address and back.
/// </summary>
/// <param name="SessionId">The session id; the client sends it back to complete the save.</param>
/// <param name="Mode">How the payer enters the method.</param>
/// <param name="ClientToken">The token the plugin's own front end part needs; set for <see cref="SaveMethodMode.EmbeddedToken"/>. Masked when printed.</param>
/// <param name="RedirectUrl">Where to send the payer; set for <see cref="SaveMethodMode.Redirect"/>.</param>
public sealed record SaveMethodSessionDto(
    string SessionId,
    SaveMethodMode Mode,
    [property: SecretMember] string? ClientToken,
    string? RedirectUrl)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
