namespace Innovayse.Application.Domains.Commands.SetRegistrarLock;

/// <summary>Command to enable or disable the registrar transfer-lock for a domain.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="Value">Whether the domain should be locked.</param>
public record SetRegistrarLockCommand(int DomainId, bool Value);
