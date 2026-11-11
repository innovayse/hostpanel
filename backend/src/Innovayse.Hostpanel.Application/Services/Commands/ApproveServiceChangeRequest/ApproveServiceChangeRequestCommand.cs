namespace Innovayse.Hostpanel.Application.Services.Commands.ApproveServiceChangeRequest;

/// <summary>Command for staff to approve a plan change request and apply it.</summary>
/// <param name="RequestId">The plan change request. Overlaid from the route.</param>
/// <param name="Note">An optional note kept with the request.</param>
public sealed record ApproveServiceChangeRequestCommand(int RequestId, string? Note);
