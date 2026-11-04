using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the email sent to a new address to confirm an email change.</summary>
/// <param name="ConfirmationLink">The absolute confirmation link, read as <c>{{ confirmation_link }}</c>.</param>
public sealed record EmailChangeTemplateModel([property: SecretMember] string ConfirmationLink) : EmailTemplateModel
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
