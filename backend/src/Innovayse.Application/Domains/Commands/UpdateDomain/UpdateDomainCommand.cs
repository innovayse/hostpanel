namespace Innovayse.Application.Domains.Commands.UpdateDomain;

/// <summary>Command to update all editable domain fields.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="FirstPaymentAmount">One-time registration cost.</param>
/// <param name="RecurringAmount">Recurring registration price.</param>
/// <param name="PaymentMethod">Payment method label (e.g. "Credit/Debit Card").</param>
/// <param name="PromotionCode">Applied promotion/coupon code.</param>
/// <param name="SubscriptionId">External payment subscription reference.</param>
/// <param name="AdminNotes">Free-text admin notes.</param>
/// <param name="ExpiresAt">ISO 8601 expiry date string.</param>
/// <param name="NextDueDate">ISO 8601 next renewal due date string.</param>
/// <param name="RegistrationPeriod">Registration period in years.</param>
/// <param name="Status">New lifecycle status name (e.g. "Active", "Expired").</param>
/// <param name="Nameservers">Optional list of nameserver hostnames to set.</param>
public record UpdateDomainCommand(
    int DomainId,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    string? PaymentMethod,
    string? PromotionCode,
    string? SubscriptionId,
    string? AdminNotes,
    string ExpiresAt,
    string NextDueDate,
    int RegistrationPeriod,
    string Status,
    List<string> Nameservers);
