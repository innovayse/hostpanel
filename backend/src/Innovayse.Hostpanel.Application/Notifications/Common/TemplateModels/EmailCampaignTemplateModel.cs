using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for a marketing campaign mail: what the admin wrote, and the link that stops further campaign mail.</summary>
/// <param name="Subject">The campaign's subject line, read as <c>{{ subject }}</c>.</param>
/// <param name="Body">The campaign's HTML body as the admin wrote it, read as <c>{{ body }}</c>.</param>
/// <param name="UnsubscribeUrl">The absolute link that unsubscribes this recipient, read as <c>{{ unsubscribe_url }}</c>.</param>
public sealed record EmailCampaignTemplateModel(string Subject, string Body, [property: SecretMember] string UnsubscribeUrl) : EmailTemplateModel
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
