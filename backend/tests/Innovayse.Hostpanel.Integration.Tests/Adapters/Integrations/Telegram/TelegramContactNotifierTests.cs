using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Infrastructure.Integrations.Telegram;
using Innovayse.Hostpanel.Infrastructure.Integrations.Telegram.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Telegram;

/// <summary>
/// Tests for <see cref="TelegramContactNotifier"/> over a scripted Bot API: it posts one plain text message per enquiry,
/// with a label for each field and no emoji, escapes what the visitor typed, leaves out the fields they did not give, and
/// stays silent when no bot is configured.
/// </summary>
public sealed class TelegramContactNotifierTests
{
    /// <summary>The enquiry most tests post.</summary>
    private static readonly ContactEnquiry Full = new("Ada Lovelace", "ada@example.com", "+37455000000", "Hosting", "Hello there", "25 Nov 2026, 10:00");

    /// <summary>Posts an enquiry through a notifier wired to a scripted Bot API.</summary>
    /// <param name="enquiry">The enquiry to post.</param>
    /// <param name="options">The bot settings.</param>
    /// <param name="handler">The scripted Bot API.</param>
    /// <returns>A task that completes once the notifier returns.</returns>
    private static async Task PostAsync(ContactEnquiry enquiry, TelegramOptions options, ScriptedHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.ApiUrl) };
        var notifier = new TelegramContactNotifier(http, Options.Create(options), NullLogger<TelegramContactNotifier>.Instance);

        await notifier.NotifyAsync(enquiry, CancellationToken.None);
    }

    /// <summary>A configured bot with a token and a chat.</summary>
    /// <returns>The settings.</returns>
    private static TelegramOptions Configured() => new() { BotToken = "token", ChatId = "42" };

    /// <summary>Reads the message text out of the one request the Bot API saw.</summary>
    /// <param name="handler">The scripted Bot API.</param>
    /// <returns>The <c>text</c> of the post.</returns>
    private static string TextOf(ScriptedHttpMessageHandler handler)
    {
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        return body.RootElement.GetProperty("text").GetString()!;
    }

    /// <summary>A full enquiry reads as labelled plain text with every field, in order, and no emoji.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FullEnquiry_IsPostedAsLabelledText()
    {
        var handler = ScriptedHttpMessageHandler.Json("{\"ok\":true}");

        await PostAsync(Full, Configured(), handler);

        handler.Requests.Single().AbsolutePath.Should().EndWith("bottoken/sendMessage");
        TextOf(handler).Should().Be(
            "*New website enquiry*\n\n*Name:* Ada Lovelace\n*Email:* ada@example.com\n*Phone:* +37455000000\n*Service:* Hosting"
            + "\n\n*Message:*\nHello there\n\n*Submitted:* 25 Nov 2026, 10:00");
    }

    /// <summary>The message holds only text a person types: no emoji and no pictograph of any kind.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Message_HoldsNoEmoji()
    {
        var handler = ScriptedHttpMessageHandler.Json("{\"ok\":true}");

        await PostAsync(Full, Configured(), handler);

        TextOf(handler).EnumerateRunes().Should().OnlyContain(rune => rune.Value < 0x2000);
    }

    /// <summary>A phone and a service the visitor left empty are not printed, labels included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingPhoneAndService_AreLeftOut()
    {
        var handler = ScriptedHttpMessageHandler.Json("{\"ok\":true}");

        await PostAsync(Full with { Phone = null, Service = null }, Configured(), handler);

        TextOf(handler).Should().NotContain("*Phone:*").And.NotContain("*Service:*").And.Contain("*Email:* ada@example.com");
    }

    /// <summary>Characters the Markdown parser reads as formatting are escaped in every visitor-supplied value.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task VisitorText_IsEscaped()
    {
        var handler = ScriptedHttpMessageHandler.Json("{\"ok\":true}");

        await PostAsync(Full with { Name = "a_b*c" }, Configured(), handler);

        TextOf(handler).Should().Contain("*Name:* a\\_b\\*c");
    }

    /// <summary>A refused post is reported to the caller, not hidden.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusedPost_IsThrown()
    {
        var handler = ScriptedHttpMessageHandler.Json("{\"ok\":false}", HttpStatusCode.BadRequest);

        var act = () => PostAsync(Full, Configured(), handler);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>With no bot configured nothing is posted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoBotConfigured_PostsNothing()
    {
        var handler = ScriptedHttpMessageHandler.Json("{\"ok\":true}");

        await PostAsync(Full, new TelegramOptions(), handler);

        handler.Requests.Should().BeEmpty();
    }
}
