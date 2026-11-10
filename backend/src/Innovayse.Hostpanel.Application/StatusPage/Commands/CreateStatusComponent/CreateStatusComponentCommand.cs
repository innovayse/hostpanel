namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusComponent;

/// <summary>Command to add an operational component to a group of the status page.</summary>
/// <param name="GroupId">The group to list the component under.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">A short explanation of what the component covers, or <c>null</c>.</param>
/// <param name="DisplayOrder">The position within the group (ascending).</param>
public sealed record CreateStatusComponentCommand(int GroupId, string Name, string? Description, int DisplayOrder);
