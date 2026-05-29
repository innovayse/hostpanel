namespace Innovayse.Application.Support.Queries.GetNetworkIssue;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Loads a single network issue and maps it to <see cref="NetworkIssueDto"/>.
/// </summary>
public sealed class GetNetworkIssueHandler(INetworkIssueRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetNetworkIssueQuery"/>.
    /// </summary>
    /// <param name="query">The get network issue query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="NetworkIssueDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the network issue is not found.</exception>
    public async Task<NetworkIssueDto> HandleAsync(GetNetworkIssueQuery query, CancellationToken ct)
    {
        var issue = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Network issue {query.Id} not found.");

        return new NetworkIssueDto(
            issue.Id,
            issue.Title,
            issue.Type.ToString(),
            issue.Server,
            issue.Priority.ToString(),
            issue.Status.ToString(),
            issue.StartDate,
            issue.EndDate,
            issue.Description,
            issue.CreatedAt);
    }
}
