namespace Innovayse.Application.Support.Commands.DeleteNetworkIssue;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="DeleteNetworkIssueCommand"/> by permanently removing the network issue.</summary>
public sealed class DeleteNetworkIssueHandler(INetworkIssueRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes a network issue.
    /// </summary>
    /// <param name="cmd">The delete network issue command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the network issue is not found.</exception>
    public async Task HandleAsync(DeleteNetworkIssueCommand cmd, CancellationToken ct)
    {
        var issue = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Network issue {cmd.Id} not found.");

        repo.Remove(issue);
        await uow.SaveChangesAsync(ct);
    }
}
