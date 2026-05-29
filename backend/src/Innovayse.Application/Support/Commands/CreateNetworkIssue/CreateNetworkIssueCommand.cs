namespace Innovayse.Application.Support.Commands.CreateNetworkIssue;

/// <summary>Command to create a new network issue.</summary>
/// <param name="Title">The issue title.</param>
/// <param name="Type">The type of network issue as a string.</param>
/// <param name="Server">The affected server name, or null.</param>
/// <param name="Priority">The priority level as a string.</param>
/// <param name="StartDate">UTC timestamp when the issue started.</param>
/// <param name="Description">HTML description of the issue.</param>
public record CreateNetworkIssueCommand(
    string Title,
    string Type,
    string? Server,
    string Priority,
    DateTimeOffset StartDate,
    string Description);
