namespace Innovayse.Application.Support.Commands.CreateNetworkIssue;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="CreateNetworkIssueCommand"/> by creating a new network issue.</summary>
public sealed class CreateNetworkIssueHandler(INetworkIssueRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new network issue and persists it.
    /// </summary>
    /// <param name="cmd">The create network issue command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created network issue.</returns>
    public async Task<int> HandleAsync(CreateNetworkIssueCommand cmd, CancellationToken ct)
    {
        var type = Enum.Parse<NetworkIssueType>(cmd.Type, true);
        var priority = Enum.Parse<NetworkIssuePriority>(cmd.Priority, true);

        var issue = NetworkIssue.Create(cmd.Title, type, cmd.Server, priority, cmd.StartDate, cmd.Description);

        repo.Add(issue);
        await uow.SaveChangesAsync(ct);

        return issue.Id;
    }
}
