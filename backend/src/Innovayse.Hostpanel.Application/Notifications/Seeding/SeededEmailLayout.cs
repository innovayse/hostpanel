using System.Text;
using Innovayse.Hostpanel.Application.Notifications.Rendering;

namespace Innovayse.Hostpanel.Application.Notifications.Seeding;

/// <summary>
/// The one branded HTML shell every seeded email uses, so each language version of each template
/// shares the same design and only the words differ.
/// <para>
/// The layout follows the brand through the <c>brand.*</c> and <c>site.*</c> objects
/// <see cref="TemplateRenderer"/> adds to every render (the logo, the button colour and the name)
/// rather than through literals, so an operator's colours reach the inbox.
/// </para>
/// </summary>
public static class SeededEmailLayout
{
    /// <summary>Builds the HTML body for one language version.</summary>
    /// <param name="copy">The words of the version.</param>
    /// <returns>The HTML body, with the Liquid variables left for the renderer.</returns>
    public static string Render(SeededEmailCopy copy)
    {
        ArgumentNullException.ThrowIfNull(copy);

        var rows = new StringBuilder();
        rows.Append($$$"""
                        <tr><td align="center" style="padding-bottom:24px;">
                          <h1 style="margin:0;font-size:22px;font-weight:700;color:#f0f0f5;">{{{copy.Heading}}}</h1>
                        </td></tr>
                        <tr><td align="center" style="padding-bottom:28px;">

            """);

        for (var i = 0; i < copy.Paragraphs.Count; i++)
        {
            // Three $: Liquid's own {{ }} must stay literal, so only {{{ }}} holes interpolate.
            var margin = i == 0 ? "0" : "8px 0 0";
            rows.Append($$$"""
                              <p style="margin:{{{margin}}};font-size:14px;color:#8a8a9a;line-height:1.6;">{{{copy.Paragraphs[i]}}}</p>

                """);
        }

        rows.Append("""
                        </td></tr>

            """);

        if (copy.ButtonUrl is not null && copy.ButtonText is not null)
        {
            rows.Append($$$"""
                        <tr><td align="center" style="padding-bottom:28px;">
                          <a href="{{{copy.ButtonUrl}}}" style="display:inline-block;padding:14px 32px;background:linear-gradient(135deg,{{ brand.primary }},{{ brand.primary_dark }});color:{{ brand.on_primary }};font-size:15px;font-weight:600;text-decoration:none;border-radius:10px;box-shadow:0 4px 20px rgba(14,165,233,0.25);">{{{copy.ButtonText}}}</a>
                        </td></tr>

            """);
        }

        if (copy.Note is not null)
        {
            var echo = copy.EchoButtonUrl && copy.ButtonUrl is not null
                ? $$$"""

                                  <p style="margin:8px 0 0;font-size:12px;color:{{ brand.primary }};word-break:break-all;">{{{copy.ButtonUrl}}}</p>
                    """
                : string.Empty;
            rows.Append($$$"""
                        <tr><td style="border-top:1px solid rgba(255,255,255,0.06);padding-top:20px;">
                          <p style="margin:0;font-size:12px;color:#5a5a6a;line-height:1.6;">{{{copy.Note}}}</p>{{{echo}}}
                        </td></tr>

            """);
        }

        return $$$"""
            <!DOCTYPE html>
            <html lang="{{{copy.Language}}}">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background-color:#0a0a0f;font-family:'Inter',system-ui,-apple-system,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#0a0a0f;padding:40px 20px;">
                <tr><td align="center">
                  <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="max-width:480px;width:100%;">
                    <tr><td align="center" style="padding-bottom:32px;">
                      {% if brand.logo_url != "" %}
                      <img src="{{ brand.logo_url }}" alt="{{ site.name }}" height="40" style="display:block;height:40px;width:auto;max-width:200px;border:0;" />
                      {% else %}
                      <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                        <td style="border:1px solid {{ brand.primary }};border-radius:10px;padding:8px 16px;">
                          <span style="font-size:16px;font-weight:700;color:{{ brand.primary }};">{{ site.name }}</span>
                        </td>
                      </tr></table>
                      {% endif %}
                    </td></tr>
                    <tr><td style="background-color:#1a1a1f;border:1px solid rgba(255,255,255,0.06);border-radius:16px;padding:40px 36px;">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
            {{{rows}}}          </table>
                    </td></tr>
                    <tr><td align="center" style="padding-top:24px;">
                      <p style="margin:0;font-size:11px;color:#3a3a4a;">© {{ site.name }}. {{{RightsReserved(copy.Language)}}}</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    /// <summary>The footer's rights line in the version's language.</summary>
    /// <param name="language">The ISO 639-1 code of the version.</param>
    /// <returns>The sentence, in English for a language with no translation.</returns>
    private static string RightsReserved(string language) => language switch
    {
        "ru" => "Все права защищены.",
        "hy" => "Բոլոր իրավունքները պաշտպանված են։",
        "de" => "Alle Rechte vorbehalten.",
        "es" => "Todos los derechos reservados.",
        _ => "All rights reserved.",
    };
}
