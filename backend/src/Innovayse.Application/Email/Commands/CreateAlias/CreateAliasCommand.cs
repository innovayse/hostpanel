namespace Innovayse.Application.Email.Commands.CreateAlias;

/// <summary>
/// Creates a new mail alias on the given email domain and provisions it on the mail server.
/// </summary>
public record CreateAliasCommand(int EmailDomainId, string SourceAddress, string DestinationAddress);
