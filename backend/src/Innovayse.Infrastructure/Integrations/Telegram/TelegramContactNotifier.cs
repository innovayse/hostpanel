namespace Innovayse.Infrastructure.Integrations.Telegram;

using System.Net.Http.Json;
using System.Text;
using Innovayse.Application.Support.Common;
using Innovayse.Application.Support.Interfaces;
using Innovayse.Infrastructure.Integrations.Telegram.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Posts contact-form enquiries to the operator's Telegram chat through the Bot API's
/// <c>sendMessage</c> method.
/// </summary>
/// <remarks>
/// <para>
/// The whole of this used to live in the public site's Nuxt server, beside the bot token it
/// needed. It is here so that container holds no credential at all; the message it renders is
/// the same message, down to the emoji and the separator rule, because that is what the operator
/// reads every day.
/// </para>
/// <para>
/// <b>Legacy <c>Markdown</c> parse mode, not <c>MarkdownV2</c>.</b> That is the mode the previous
/// implementation used and <see cref="EscapeMarkdown"/> escapes exactly the character set it
/// escaped. Moving to <c>MarkdownV2</c> would be a different escape set and a message that reads
/// differently, which is not what a move of the credential is allowed to change.
/// </para>
/// <para>
/// Failures are thrown, not swallowed, as
/// <see cref="IContactNotifier"/> requires: only the caller knows whether the mail was already
/// delivered and the post is therefore survivable. The one case handled here is the deployment
/// that configured no bot, which this class can see and the caller cannot.
/// </para>
/// </remarks>
/// <param name="http">The typed <see cref="HttpClient"/> configured by <c>IHttpClientFactory</c>.</param>
/// <param name="options">Bound <see cref="TelegramOptions"/>.</param>
/// <param name="logger">Logger, for the unconfigured case -- the only outcome nothing else records.</param>
public sealed class TelegramContactNotifier(
    HttpClient http,
    IOptions<TelegramOptions> options,
    ILogger<TelegramContactNotifier> logger) : IContactNotifier
{
    /// <summary>
    /// The characters Telegram's legacy Markdown parser treats as formatting, each escaped with a
    /// backslash before the message is sent.
    /// </summary>
    /// <remarks>
    /// Carried over character for character from the Nuxt route's regular expression. It is wider
    /// than the legacy parser strictly needs -- that mode only acts on <c>_ * [</c> and a
    /// backtick -- and stays wide on purpose: a name containing an underscore did not break the
    /// message before this move and must not now, and narrowing the set is a behaviour change
    /// dressed as a tidy-up.
    /// </remarks>
    private const string MarkdownSpecials = "_*[]()~`>#+=|{}!";

    /// <summary>Posts one enquiry to the configured Telegram chat.</summary>
    /// <param name="enquiry">The validated submission.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A task that completes when Telegram has accepted the post -- or immediately, having logged
    /// it, when this deployment configured no bot.
    /// </returns>
    /// <exception cref="HttpRequestException">
    /// Thrown when Telegram refuses the post or cannot be reached. The caller decides what that
    /// means; here it is reported rather than hidden.
    /// </exception>
    public async Task NotifyAsync(ContactEnquiry enquiry, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            // Information, not a warning: a deployment that runs no bot is a supported
            // configuration, not a fault. It is still written down, so "the enquiry never reached
            // Telegram" has an answer in the log instead of being invisible.
            logger.LogInformation(
                "Contact enquiry not posted to Telegram: the \"{Section}\" configuration section is unset. "
                + "The enquiry was delivered by email.",
                TelegramOptions.SectionName);

            return;
        }

        // The token is a path segment of the Bot API, so it cannot be part of the client's base
        // address -- that is set once at registration, while this is read per call from options.
        var url = $"bot{settings.BotToken}/sendMessage";
        var payload = new
        {
            chat_id = settings.ChatId,
            text = BuildMessage(enquiry),
            parse_mode = "Markdown",
        };

        using var response = await http.PostAsJsonAsync(url, payload, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Renders the enquiry as the Markdown body of the Telegram post.</summary>
    /// <param name="enquiry">The validated submission.</param>
    /// <returns>The message text, with every visitor-supplied value escaped.</returns>
    private static string BuildMessage(ContactEnquiry enquiry)
    {
        var sb = new StringBuilder();
        sb.Append("🔔 *New website enquiry*\n\n");
        sb.Append("👤 *Name:* ").Append(EscapeMarkdown(enquiry.Name)).Append('\n');
        sb.Append("📧 *Email:* ").Append(EscapeMarkdown(enquiry.Email));

        if (enquiry.Phone is not null)
        {
            sb.Append("\n📱 *Phone:* ").Append(EscapeMarkdown(enquiry.Phone));
        }

        if (enquiry.Service is not null)
        {
            sb.Append("\n🛠 *Service:* ").Append(EscapeMarkdown(enquiry.Service));
        }

        sb.Append("\n\n💬 *Message:*\n").Append(EscapeMarkdown(enquiry.Message));
        sb.Append("\n\n───────────────\n📅 ").Append(EscapeMarkdown(enquiry.SubmittedAt));
        return sb.ToString();
    }

    /// <summary>
    /// Escapes the characters Telegram's Markdown parser would otherwise read as formatting.
    /// </summary>
    /// <param name="text">Text as the visitor typed it.</param>
    /// <returns>The same text, safe to interpolate into a Markdown message.</returns>
    private static string EscapeMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (MarkdownSpecials.Contains(ch, StringComparison.Ordinal))
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}
