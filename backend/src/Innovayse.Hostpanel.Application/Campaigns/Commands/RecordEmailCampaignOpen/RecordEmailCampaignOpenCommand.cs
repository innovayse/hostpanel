using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignOpen;

/// <summary>
/// Command to count one load of a campaign mail's tracking pixel. Public: the mail client fetches the image with no session,
/// so the token in its address alone names the recipient.
/// </summary>
/// <param name="Token">The recipient's tracking token from the pixel address; a secret, so it never reaches a log line.</param>
public sealed record RecordEmailCampaignOpenCommand([property: SecretMember] string Token)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
