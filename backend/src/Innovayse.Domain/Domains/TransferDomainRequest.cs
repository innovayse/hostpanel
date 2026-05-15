namespace Innovayse.Domain.Domains;

/// <summary>
/// Parameters required to initiate an incoming domain transfer via a registrar provider.
/// </summary>
/// <param name="DomainName">Fully-qualified domain name to transfer (e.g. "example.com").</param>
/// <param name="EppCode">Authorization/EPP code obtained from the losing registrar.</param>
/// <param name="WhoisPrivacy">Whether to enable WHOIS privacy after the transfer completes.</param>
public record TransferDomainRequest(string DomainName, string EppCode, bool WhoisPrivacy);
