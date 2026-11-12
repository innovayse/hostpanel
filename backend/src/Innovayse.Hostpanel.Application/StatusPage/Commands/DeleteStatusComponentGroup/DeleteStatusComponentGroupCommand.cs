namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusComponentGroup;

/// <summary>Command to remove a status page group that has no components.</summary>
/// <param name="Id">The group identifier.</param>
public sealed record DeleteStatusComponentGroupCommand(int Id);
