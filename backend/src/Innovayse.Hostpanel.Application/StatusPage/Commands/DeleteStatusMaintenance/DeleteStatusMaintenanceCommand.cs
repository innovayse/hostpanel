namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusMaintenance;

/// <summary>Command to remove a maintenance window, whatever its state.</summary>
/// <param name="Id">The window identifier.</param>
public sealed record DeleteStatusMaintenanceCommand(int Id);
