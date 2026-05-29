namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a network issue.</summary>
/// <param name="Id">Network issue primary key.</param>
/// <param name="Title">The issue title.</param>
/// <param name="Type">The type of network issue as a string.</param>
/// <param name="Server">The affected server name, if applicable.</param>
/// <param name="Priority">The priority level as a string.</param>
/// <param name="Status">The current lifecycle status as a string.</param>
/// <param name="StartDate">UTC timestamp when the issue started.</param>
/// <param name="EndDate">UTC timestamp when the issue was resolved, if applicable.</param>
/// <param name="Description">HTML description of the issue.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
public record NetworkIssueDto(
    int Id,
    string Title,
    string Type,
    string? Server,
    string Priority,
    string Status,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string Description,
    DateTimeOffset CreatedAt);
