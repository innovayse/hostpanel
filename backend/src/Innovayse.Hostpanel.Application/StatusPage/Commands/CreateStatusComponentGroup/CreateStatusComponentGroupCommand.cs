namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusComponentGroup;

/// <summary>Command to create a heading of the status page that components are listed under.</summary>
/// <param name="Name">The display name.</param>
/// <param name="DisplayOrder">The position among the groups (ascending).</param>
public sealed record CreateStatusComponentGroupCommand(string Name, int DisplayOrder);
