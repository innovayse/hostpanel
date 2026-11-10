namespace Innovayse.Hostpanel.Application.StatusPage.Commands.OpenStatusIncident;

/// <summary>Command to announce an incident on the status page.</summary>
/// <param name="Title">The headline visitors see.</param>
/// <param name="Message">The first thing the visitors are told.</param>
public sealed record OpenStatusIncidentCommand(string Title, string Message);
