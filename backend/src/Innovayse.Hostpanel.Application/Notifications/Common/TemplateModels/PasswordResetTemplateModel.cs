using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>user-password-reset</c> email.</summary>
/// <param name="ResetLink">The absolute password reset link, read as <c>{{ reset_link }}</c>.</param>
public sealed record PasswordResetTemplateModel([property: SecretMember] string ResetLink) : EmailTemplateModel
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
