namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusIncident;

/// <summary>Command to remove an incident with its timeline.</summary>
/// <param name="Id">The incident identifier.</param>
public sealed record DeleteStatusIncidentCommand(int Id);
