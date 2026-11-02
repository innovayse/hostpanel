using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;

/// <summary>
/// Sends an email by rendering the requested template and delegating to <see cref="IEmailSender"/>.
/// Always writes an <see cref="EmailLog"/> entry regardless of send outcome.
/// </summary>
/// <remarks>
/// A command carrying an idempotency key is sent at most once per recipient: each address gets its own
/// log row keyed by <see cref="EmailIdempotencyKey.ForRecipient"/>, claimed (state Sending) and saved
/// before the relay is called, so a replay of the same message finds the row and skips the address.
/// The one remaining window is a crash after the relay accepted and before the outcome was saved: the
/// row stays Sending, and after <see cref="NotificationOptions.DeliveryClaimSeconds"/> the next replay
/// sends that address once more.
/// </remarks>
public sealed class SendEmailHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The mail relay this platform does not own; failures are recorded, never thrown.</summary>
    private readonly IEmailSender _emailSender;

    /// <summary>Renders a stored template's Liquid syntax against the caller's data model.</summary>
    private readonly TemplateRenderer _renderer;

    /// <summary>Records a missing template or an undelivered message, which every fire-and-forget caller would otherwise lose.</summary>
    private readonly ILogger<SendEmailHandler> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>The Wolverine bus, for the addresses of the contacts a message is copied to.</summary>
    private readonly IMessageBus _bus;

    /// <summary>How long a keyed delivery claimed by another attempt is trusted to still be in flight.</summary>
    private readonly IOptions<NotificationOptions> _options;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="emailSender">The mail relay this platform does not own.</param>
    /// <param name="renderer">Renders a stored template against the caller's data model.</param>
    /// <param name="logger">Records a refusal no caller reads.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="bus">The Wolverine bus, for the addresses of the contacts a message is copied to.</param>
    /// <param name="options">How long a keyed delivery claimed by another attempt is trusted to still be in flight.</param>
    public SendEmailHandler(
        IHostpanelDbContext context,
        IEmailSender emailSender,
        TemplateRenderer renderer,
        ILogger<SendEmailHandler> logger,
        IClock clock,
        IMessageBus bus,
        IOptions<NotificationOptions> options)
    {
        _context = context;
        _emailSender = emailSender;
        _renderer = renderer;
        _logger = logger;
        _clock = clock;
        _bus = bus;
        _options = options;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The recipient, template slug and data model to render.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the attempt has been logged, whether or not the relay accepted the message:
    /// a delivery failure is recorded on the log entry, not answered as a refusal.
    /// <c>EMAIL_TEMPLATE_NOT_FOUND</c> when the slug has no version in the
    /// recipient's language nor in the default language.
    /// </returns>
    public async Task<Result> HandleAsync(SendEmailCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // The recipient's version when one is written, the default language's otherwise: a template
        // nobody has translated yet still reaches the recipient rather than being lost.
        var language = SupportedLanguages.IsSupported(cmd.Language?.ToLowerInvariant())
            ? cmd.Language!.ToLowerInvariant()
            : SupportedLanguages.Default;
        var versions = await _context.EmailTemplates
            .Where(t => t.Slug == cmd.TemplateSlug
                && (t.Language == language || t.Language == SupportedLanguages.Default))
            .ToListAsync(ct);
        var template = versions.Find(t => t.Language == language)
            ?? versions.Find(t => t.Language == SupportedLanguages.Default);
        if (template is null)
        {
            // Before the Result conversion this threw and surfaced in the log; every event handler
            // dispatches this and ignores the answer, so without this line the mail is lost silently.
            _logger.LogWarning("Email template {TemplateSlug} was not found; nothing was sent", cmd.TemplateSlug);
            return Result.FailureCode(HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
        }

        if (!template.IsActive)
        {
            return Result.Success();
        }

        var subject = await _renderer.RenderTextAsync(template.Subject, cmd.TemplateData, ct);
        var body = await _renderer.RenderHtmlAsync(template.Body, cmd.TemplateData, ct);

        await DeliverAsync(cmd.To, cmd.TemplateSlug, subject, body, cmd.IdempotencyKey, ct);

        // The contacts who switched this kind of mail on get the same message. Each copy is its own
        // delivery and its own log line, and an address already written to is not mailed twice.
        if (cmd.Copy is { } copy)
        {
            var contacts = await _bus.ListContactNotificationEmailsAsync(copy.ClientId, copy.Topic, ct);
            foreach (var address in contacts.Where(a => !string.Equals(a, cmd.To, StringComparison.OrdinalIgnoreCase)))
            {
                await DeliverAsync(address, cmd.TemplateSlug, subject, body, cmd.IdempotencyKey, ct);
            }
        }

        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>Delivers one rendered message to one address, keyed when the command carries a key.</summary>
    /// <param name="to">The recipient address.</param>
    /// <param name="templateSlug">The template the message was rendered from, for the log.</param>
    /// <param name="subject">The rendered subject.</param>
    /// <param name="body">The rendered HTML body.</param>
    /// <param name="messageKey">The command's idempotency key; null sends without a claim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the address was delivered to, skipped, or staged for saving.</returns>
    private Task DeliverAsync(
        string to, string templateSlug, string subject, string body, string? messageKey, CancellationToken ct)
    {
        return messageKey is null
            ? DeliverUnkeyedAsync(to, templateSlug, subject, body, ct)
            : DeliverOnceAsync(to, templateSlug, subject, body, EmailIdempotencyKey.ForRecipient(messageKey, to), ct);
    }

    /// <summary>
    /// Sends one keyed message at most once: claims (or reclaims) its log row and saves the claim before
    /// the relay is called, then saves the outcome. Every step is saved here, not by the caller, because
    /// the claim must be visible to a concurrent or replayed delivery before the mail leaves.
    /// </summary>
    /// <param name="to">The recipient address.</param>
    /// <param name="templateSlug">The template the message was rendered from, for the log.</param>
    /// <param name="subject">The rendered subject.</param>
    /// <param name="body">The rendered HTML body.</param>
    /// <param name="key">This recipient's idempotency key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the outcome is saved, or at once when another delivery owns it.</returns>
    private async Task DeliverOnceAsync(
        string to, string templateSlug, string subject, string body, string key, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var log = await _context.EmailLogs.SingleOrDefaultAsync(l => l.IdempotencyKey == key, ct);
        if (log is null)
        {
            log = EmailLog.Claim(to, subject, body, key, now);
            _context.EmailLogs.Add(log);
        }
        else if (log.MayResend(now, _options.Value.DeliveryClaimLease))
        {
            log.Reclaim(now);
        }
        else
        {
            // Sent already, or claimed by a delivery still inside its lease: this replay adds nothing.
            _logger.LogInformation(
                "Email from template {TemplateSlug} was already sent or is being sent; skipped", templateSlug);
            return;
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // The unique key (a concurrent first claim) or the row version (a concurrent reclaim) says
            // another delivery owns this address; it sends, this one must not.
            _context.DetachAll();
            _logger.LogInformation(
                "Email from template {TemplateSlug} was claimed by another delivery; skipped", templateSlug);
            return;
        }

        var sent = await _emailSender.SendAsync(new EmailMessage(to, subject, body), ct);
        if (sent.Failure is { } failure)
        {
            _logger.LogWarning(
                "Email from template {TemplateSlug} was not delivered: {Failure}", templateSlug, failure);
            log.MarkUndelivered(failure, _clock.UtcNow);
        }
        else
        {
            log.MarkDelivered(_clock.UtcNow);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Our lease ran out mid-send and another delivery reclaimed the row; its outcome is the one kept.
            _context.DetachAll();
            _logger.LogWarning(
                "Email from template {TemplateSlug} was reclaimed by another delivery before its outcome was saved",
                templateSlug);
        }
    }

    /// <summary>Hands one rendered message to the relay and stages its log line, as for every unkeyed send.</summary>
    /// <param name="to">The recipient address.</param>
    /// <param name="templateSlug">The template the message was rendered from, for the log.</param>
    /// <param name="subject">The rendered subject.</param>
    /// <param name="body">The rendered HTML body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the relay has answered; the log line is saved by the caller.</returns>
    private async Task DeliverUnkeyedAsync(
        string to, string templateSlug, string subject, string body, CancellationToken ct)
    {
        // The relay is a port this platform does not own; a failed send is recorded on the log
        // entry as its classified reason rather than turned into a refusal, so a caller that fires
        // this command and forgets it (every event handler in Notifications/Events does) never
        // sees a mail server being unreachable. The relay's own wording is logged by the adapter.
        var sent = await _emailSender.SendAsync(new EmailMessage(to, subject, body), ct);
        if (!sent.Delivered)
        {
            _logger.LogWarning("Email from template {TemplateSlug} was not delivered: {Failure}", templateSlug, sent.Failure);
        }

        _context.EmailLogs.Add(EmailLog.Create(to, subject, body, sent.Failure, _clock.UtcNow));
    }
}
