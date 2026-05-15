namespace Innovayse.Application.Domains.Commands.SetDnsManagement;

/// <summary>Command to enable or disable DNS management for a domain.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="Enabled">Whether DNS management should be enabled.</param>
public record SetDnsManagementCommand(int DomainId, bool Enabled);
