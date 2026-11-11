namespace Innovayse.Hostpanel.Application.Services.Commands.RejectServiceChangeRequest;

/// <summary>Command for staff to turn a plan change request down.</summary>
/// <param name="RequestId">The plan change request. Overlaid from the route.</param>
/// <param name="Note">An optional note kept with the request, for example the reason.</param>
public sealed record RejectServiceChangeRequestCommand(int RequestId, string? Note);
