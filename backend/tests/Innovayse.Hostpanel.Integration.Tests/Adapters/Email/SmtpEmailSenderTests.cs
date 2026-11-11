using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Infrastructure.Email;
using Innovayse.Hostpanel.Infrastructure.Email.Enums;
using Innovayse.Hostpanel.Infrastructure.Email.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Email;

/// <summary>
/// Tests for the SMTP adapter's answer when a message is not accepted: a classified reason, never
/// a thrown exception and never the relay's own wording.
/// </summary>
public sealed class SmtpEmailSenderTests
{
    /// <summary>Builds the adapter against a local port nothing listens on.</summary>
    /// <returns>The adapter under test.</returns>
    private static SmtpEmailSender CreateAgainstClosedPort() =>
        new(
            Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = 1,
                Username = string.Empty,
                Password = string.Empty,
                Encryption = SmtpEncryption.None,
                From = new SmtpFromOptions { Address = "panel@example.test", Name = "Panel" },
            }),
            NullLogger<SmtpEmailSender>.Instance);

    /// <summary>A relay that refuses the connection answers <see cref="EmailSendFailure.RelayUnreachable"/>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SendAsync_RelayRefusesConnection_AnswersRelayUnreachable()
    {
        var result = await CreateAgainstClosedPort().SendAsync(
            new EmailMessage("someone@example.test", "Subject", "<p>Body</p>"), CancellationToken.None);

        Assert.False(result.Delivered);
        Assert.Equal(EmailSendFailure.RelayUnreachable, result.Failure);
    }

    /// <summary>A recipient that is not an address answers <see cref="EmailSendFailure.RecipientRejected"/> before any connection.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SendAsync_UnreadableRecipient_AnswersRecipientRejected()
    {
        var result = await CreateAgainstClosedPort().SendAsync(
            new EmailMessage("not an address <<", "Subject", "<p>Body</p>"), CancellationToken.None);

        Assert.Equal(EmailSendFailure.RecipientRejected, result.Failure);
    }

    /// <summary>A marketing message is marked with the <c>List-Unsubscribe</c> header holding its one-click address and the RFC 8058 <c>List-Unsubscribe-Post</c> header.</summary>
    [Fact]
    public void Compose_MessageWithUnsubscribeUrl_CarriesTheOneClickListHeaders()
    {
        var mime = CreateAgainstClosedPort().Compose(
            new EmailMessage("someone@example.test", "Subject", "<p>Body</p>", UnsubscribeUrl: "https://portal.example/api/public/unsubscribe/one-click?token=abc"));

        Assert.Equal("<https://portal.example/api/public/unsubscribe/one-click?token=abc>", mime.Headers["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", mime.Headers["List-Unsubscribe-Post"]);
    }

    /// <summary>A message that is not marketing carries no list headers.</summary>
    [Fact]
    public void Compose_MessageWithoutUnsubscribeUrl_CarriesNoListHeaders()
    {
        var mime = CreateAgainstClosedPort().Compose(new EmailMessage("someone@example.test", "Subject", "<p>Body</p>"));

        Assert.Null(mime.Headers["List-Unsubscribe"]);
        Assert.Null(mime.Headers["List-Unsubscribe-Post"]);
    }
}
