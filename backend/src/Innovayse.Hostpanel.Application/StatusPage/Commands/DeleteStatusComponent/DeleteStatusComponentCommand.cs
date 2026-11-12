namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusComponent;

/// <summary>Command to remove a status page component with its check records.</summary>
/// <param name="Id">The component identifier.</param>
public sealed record DeleteStatusComponentCommand(int Id);
