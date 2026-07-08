namespace Innovayse.Application.Email.Commands.VerifyEmailDomainDns;

/// <summary>
/// Triggers live DNS verification for an email domain.
/// If all required records pass, the domain is activated automatically.
/// </summary>
public record VerifyEmailDomainDnsCommand(int EmailDomainId);
