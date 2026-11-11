namespace Innovayse.Hostpanel.Application.Services.Commands.CompleteServiceChange;

/// <summary>
/// Applies an approved plan change: the hosting panel gets the new package, the service moves to the new plan,
/// and a downgrade's credit goes to the client's balance. The one path both the automatic and the staff approval take.
/// </summary>
/// <param name="RequestId">The approved plan change request.</param>
public sealed record CompleteServiceChangeCommand(int RequestId);
