namespace Innovayse.Application.Email.Commands.CreateEmailDomain;

using Innovayse.Application.Common;
using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="CreateEmailDomainCommand"/>.
/// Creates the aggregate, provisions the domain on the mail server, generates DKIM keys,
/// and persists everything in a single unit of work.
/// </summary>
public sealed class CreateEmailDomainHandler(
    IEmailDomainRepository repo,
    IMailServerClient mailServer,
    IUnitOfWork uow)
{
    /// <summary>
    /// Provisions a new email domain.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A DTO representing the newly created email domain.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an email domain for the given domain name already exists.
    /// </exception>
    public async Task<EmailDomainDto> HandleAsync(CreateEmailDomainCommand cmd, CancellationToken ct)
    {
        // Guard against duplicates
        var existing = await repo.FindByDomainNameAsync(cmd.DomainName, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Email is already configured for {cmd.DomainName}.");

        // Build the aggregate
        var emailDomain = EmailDomain.Create(
            cmd.ClientId, cmd.DomainName,
            cmd.MaxMailboxes, cmd.MaxAliases, cmd.MaxQuotaMb,
            cmd.HostpanelDomainId);

        // Provision domain on the mail server
        var mailcowRef = await mailServer.CreateDomainAsync(
            cmd.DomainName, cmd.MaxQuotaMb, cmd.MaxMailboxes, ct);
        emailDomain.SetMailcowRef(mailcowRef);

        // Generate DKIM key pair
        var dkimPublicKey = await mailServer.GenerateDkimAsync(cmd.DomainName, 2048, ct);
        if (dkimPublicKey is not null)
            emailDomain.SetDkimPublicKey(dkimPublicKey);

        repo.Add(emailDomain);
        await uow.SaveChangesAsync(ct);

        return EmailDomainDto.From(emailDomain);
    }
}
