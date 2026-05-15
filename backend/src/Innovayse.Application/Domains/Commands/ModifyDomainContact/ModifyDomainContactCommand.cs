namespace Innovayse.Application.Domains.Commands.ModifyDomainContact;

using Innovayse.Domain.Domains;

/// <summary>Command to modify the WHOIS registrant contact details at the registrar.</summary>
/// <param name="DomainId">Primary key of the domain whose contact to modify.</param>
/// <param name="Contact">The updated registrant contact details.</param>
public record ModifyDomainContactCommand(int DomainId, DomainContact Contact);
