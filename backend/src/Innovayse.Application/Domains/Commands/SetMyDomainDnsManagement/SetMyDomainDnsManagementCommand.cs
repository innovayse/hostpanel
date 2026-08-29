namespace Innovayse.Application.Domains.Commands.SetMyDomainDnsManagement;

/// <summary>Command for a client to enable or disable DNS management on one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>SetDnsManagementCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="Enabled">Whether DNS management should be enabled.</param>
public sealed record SetMyDomainDnsManagementCommand(
    int DomainId,
    bool Enabled);
