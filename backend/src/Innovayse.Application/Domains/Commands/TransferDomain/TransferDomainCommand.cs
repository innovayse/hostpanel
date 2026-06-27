namespace Innovayse.Application.Domains.Commands.TransferDomain;

/// <summary>Command to initiate an incoming domain transfer for a client.</summary>
/// <param name="ClientId">The client requesting the transfer.</param>
/// <param name="DomainName">Fully-qualified domain name to transfer.</param>
/// <param name="EppCode">Authorization/EPP code obtained from the losing registrar.</param>
/// <param name="WhoisPrivacy">Whether to enable WHOIS privacy after the transfer completes.</param>
/// <param name="FirstPaymentAmount">One-time transfer cost to record on the domain.</param>
/// <param name="RecurringAmount">Recurring renewal price to record on the domain.</param>
/// <param name="PaymentMethod">Payment method label to record on the domain; null if not set.</param>
public record TransferDomainCommand(
    int ClientId,
    string DomainName,
    string EppCode,
    bool WhoisPrivacy,
    decimal FirstPaymentAmount = 0,
    decimal RecurringAmount = 0,
    string? PaymentMethod = null);
