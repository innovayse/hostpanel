using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// Seeds the "email-campaign" template, in every supported language: the frame every marketing campaign mail is sent in,
/// the admin's own HTML in the middle and the unsubscribe link under it. Each language version is seeded once and never
/// overwritten, so an operator's edit of the frame survives a restart.
/// </summary>
public static class EmailCampaignTemplateSeeder
{
    /// <summary>The template slug every campaign mail is sent with.</summary>
    public const string Slug = "email-campaign";

    /// <summary>The description shown beside the template in the admin template list.</summary>
    private const string Description = "The frame of every marketing campaign mail: the campaign body and the unsubscribe link.";

    /// <summary>Ensures every language version of the campaign template exists, creating the missing ones.</summary>
    /// <param name="context">The data context the template rows live in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every version exists.</returns>
    public static Task EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        return EmailTemplateSeeding.AddMissingAsync(context, Versions(), ct);
    }

    /// <summary>Builds the template rows, one per supported language.</summary>
    /// <returns>The English, Russian and Armenian versions.</returns>
    private static IReadOnlyList<EmailTemplate> Versions()
    {
        return
        [
            Version("en", "You receive this email because you agreed to news and offers from {{ site.name }}.", "Unsubscribe"),
            Version("ru", "Вы получили это письмо, потому что согласились получать новости и предложения {{ site.name }}.", "Отписаться"),
            Version("hy", "Դուք ստանում եք այս նամակը, քանի որ համաձայնել եք ստանալ {{ site.name }}-ի նորություններն ու առաջարկները։", "Ապաբաժանորդագրվել"),
        ];
    }

    /// <summary>Builds one language version: the subject is the campaign's, the body its HTML above a footer with the unsubscribe link.</summary>
    /// <param name="language">The language code of the version.</param>
    /// <param name="reason">The sentence that tells the recipient why the mail arrived.</param>
    /// <param name="unsubscribe">The text of the unsubscribe link.</param>
    /// <returns>The template row.</returns>
    private static EmailTemplate Version(string language, string reason, string unsubscribe)
    {
        // The body is the admin's own HTML, so it is printed raw; every other value stays encoded.
        var html = $$$"""
            <!DOCTYPE html>
            <html lang="{{{language}}}">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background-color:#f4f4f7;font-family:system-ui,-apple-system,'Segoe UI',sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f7;padding:24px 12px;">
                <tr><td align="center">
                  <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background-color:#ffffff;border-radius:8px;">
                    <tr><td style="padding:24px;font-size:15px;line-height:1.6;color:#222222;">{{ body | raw }}</td></tr>
                    <tr><td style="padding:16px 24px;border-top:1px solid #e5e5ea;font-size:12px;line-height:1.5;color:#6b6b76;">
                      {{{reason}}} <a href="{{ unsubscribe_url }}" style="color:#6b6b76;">{{{unsubscribe}}}</a>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        return EmailTemplate.Create(Slug, language, "{{ subject }}", html, Description);
    }
}
