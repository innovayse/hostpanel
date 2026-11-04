using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the invitation to manage a client account.</summary>
/// <param name="InviteLink">The absolute link that accepts the invitation, read as <c>{{ invite_link }}</c>.</param>
/// <param name="ClientName">The invited-to client's full name, read as <c>{{ client_name }}</c>.</param>
/// <param name="FirstName">The invitee's first name, read as <c>{{ first_name }}</c>.</param>
public sealed record UserInviteTemplateModel([property: SecretMember] string InviteLink, string ClientName, string FirstName) : EmailTemplateModel
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
