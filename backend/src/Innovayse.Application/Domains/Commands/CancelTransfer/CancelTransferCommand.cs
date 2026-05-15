namespace Innovayse.Application.Domains.Commands.CancelTransfer;

/// <summary>Command to cancel a pending incoming domain transfer.</summary>
/// <param name="DomainId">Primary key of the domain whose transfer should be cancelled.</param>
public record CancelTransferCommand(int DomainId);
