namespace Innovayse.Application.Email.Commands.DeleteAlias;

using Innovayse.Application.Common;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="DeleteAliasCommand"/>.
/// Deprovisions the alias on the mail server, removes it from the aggregate, and persists.
/// </summary>
public sealed class DeleteAliasHandler(
    IEmailDomainRepository repo,
    IMailServerClient mailServer,
    IUnitOfWork uow)
{
    /// <summary>
    /// Deletes an existing mail alias from the specified email domain.
    /// </summary>
    /// <param name="cmd">The delete alias command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email domain or alias is not found.
    /// </exception>
    public async Task HandleAsync(DeleteAliasCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {cmd.EmailDomainId} not found.");

        var alias = domain.Aliases.FirstOrDefault(a => a.Id == cmd.AliasId)
            ?? throw new InvalidOperationException($"Alias {cmd.AliasId} not found.");

        await mailServer.DeleteAliasAsync(alias.Id, ct);

        domain.RemoveAlias(cmd.AliasId);
        await uow.SaveChangesAsync(ct);
    }
}
