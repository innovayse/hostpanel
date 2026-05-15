namespace Innovayse.Application.Domains.Commands.SetEmailForwarding;

/// <summary>Command to enable or disable email forwarding for a domain.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="Enabled">Whether email forwarding should be enabled.</param>
public record SetEmailForwardingCommand(int DomainId, bool Enabled);
