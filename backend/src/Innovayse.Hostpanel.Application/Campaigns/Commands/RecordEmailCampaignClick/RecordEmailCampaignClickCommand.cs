using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignClick;

/// <summary>
/// Command to count one follow of a tracked link of a campaign mail and learn where it leads. Public: the reader clicks from a
/// mail with no session, so the token alone names the recipient. The destination is never part of the command: it is looked up
/// from the campaign's stored body by the link index.
/// </summary>
/// <param name="Token">The recipient's tracking token from the link; a secret, so it never reaches a log line.</param>
/// <param name="LinkIndex">The position of the clicked link among the trackable links of the campaign body, counted from zero.</param>
public sealed record RecordEmailCampaignClickCommand([property: SecretMember] string Token, int LinkIndex)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
