namespace Innovayse.Application.Support.Commands.UpdateNetworkIssue;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="UpdateNetworkIssueCommand"/> by updating the network issue fields.</summary>
public sealed class UpdateNetworkIssueHandler(INetworkIssueRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates an existing network issue.
    /// </summary>
    /// <param name="cmd">The update network issue command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the network issue is not found.</exception>
    public async Task HandleAsync(UpdateNetworkIssueCommand cmd, CancellationToken ct)
    {
        var issue = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Network issue {cmd.Id} not found.");

        var type = Enum.Parse<NetworkIssueType>(cmd.Type, true);
        var priority = Enum.Parse<NetworkIssuePriority>(cmd.Priority, true);
        var status = Enum.Parse<NetworkIssueStatus>(cmd.Status, true);

        issue.Update(cmd.Title, type, cmd.Server, priority, status, cmd.StartDate, cmd.EndDate, cmd.Description);
        await uow.SaveChangesAsync(ct);
    }
}
