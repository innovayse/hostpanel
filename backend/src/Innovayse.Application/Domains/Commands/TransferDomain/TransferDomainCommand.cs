namespace Innovayse.Application.Domains.Commands.TransferDomain;

/// <summary>Command to initiate an incoming domain transfer for a client.</summary>
/// <param name="ClientId">The client requesting the transfer.</param>
/// <param name="DomainName">Fully-qualified domain name to transfer.</param>
/// <param name="EppCode">Authorization/EPP code obtained from the losing registrar.</param>
/// <param name="WhoisPrivacy">Whether to enable WHOIS privacy after the transfer completes.</param>
public record TransferDomainCommand(int ClientId, string DomainName, string EppCode, bool WhoisPrivacy);
