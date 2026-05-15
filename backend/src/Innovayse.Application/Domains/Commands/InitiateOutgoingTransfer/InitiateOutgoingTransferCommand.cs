namespace Innovayse.Application.Domains.Commands.InitiateOutgoingTransfer;

/// <summary>Command to initiate an outgoing transfer of a domain to another registrar.</summary>
/// <param name="DomainId">Primary key of the domain to transfer out.</param>
public record InitiateOutgoingTransferCommand(int DomainId);
