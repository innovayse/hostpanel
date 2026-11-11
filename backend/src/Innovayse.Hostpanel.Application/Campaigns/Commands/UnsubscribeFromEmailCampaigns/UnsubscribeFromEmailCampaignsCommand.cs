using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.UnsubscribeFromEmailCampaigns;

/// <summary>
/// Command to stop campaign mail to the client a mail's unsubscribe link belongs to. Public: the reader follows the link from
/// a mail and has no session, so the token alone names the client.
/// </summary>
/// <param name="Token">The personal token from the unsubscribe link of one campaign mail; a secret, so it never reaches a log line.</param>
public sealed record UnsubscribeFromEmailCampaignsCommand([property: SecretMember] string Token)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
