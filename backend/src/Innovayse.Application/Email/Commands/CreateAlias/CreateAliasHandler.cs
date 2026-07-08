namespace Innovayse.Application.Email.Commands.CreateAlias;

using Innovayse.Application.Common;
using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="CreateAliasCommand"/>.
/// Adds the alias to the aggregate, provisions it on the mail server, and persists.
/// </summary>
public sealed class CreateAliasHandler(
    IEmailDomainRepository repo,
    IMailServerClient mailServer,
    IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new mail alias on the specified email domain.
    /// </summary>
    /// <param name="cmd">The create alias command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A DTO representing the newly created mail alias.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email domain is not found or the domain's business rules are violated.
    /// </exception>
    public async Task<MailAliasDto> HandleAsync(CreateAliasCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {cmd.EmailDomainId} not found.");

        var alias = domain.AddAlias(cmd.SourceAddress, cmd.DestinationAddress);

        await mailServer.CreateAliasAsync(cmd.SourceAddress, cmd.DestinationAddress, ct);
        await uow.SaveChangesAsync(ct);

        return MailAliasDto.From(alias);
    }
}
