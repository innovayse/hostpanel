namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CancelStatusMaintenance;

/// <summary>Command to call off a maintenance window that has not ended.</summary>
/// <param name="Id">The window identifier.</param>
public sealed record CancelStatusMaintenanceCommand(int Id);
