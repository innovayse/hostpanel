namespace Innovayse.Domain.Notifications;

/// <summary>Represents an email message to be sent.</summary>
/// <param name="To">The recipient email address.</param>
/// <param name="Subject">The email subject line.</param>
/// <param name="Body">The email body content.</param>
/// <param name="IsHtml">Whether the body is HTML. Defaults to <see langword="true"/>.</param>
public record EmailMessage(string To, string Subject, string Body, bool IsHtml = true);
