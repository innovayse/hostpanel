namespace Innovayse.Application.Domains.Queries.GetDomain;

using System.Linq;
using Innovayse.Application.Domains.DTOs;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>Returns a full <see cref="DomainDto"/> including nameservers and DNS records.</summary>
public sealed class GetDomainHandler(IDomainRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetDomainQuery"/>.
    /// </summary>
    /// <param name="query">The get domain query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The domain DTO with nameservers and DNS records.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the domain is not found.</exception>
    public async Task<DomainDto> HandleAsync(GetDomainQuery query, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(query.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {query.DomainId} not found.");

        return new DomainDto(
            domain.Id,
            domain.ClientId,
            domain.Name,
            domain.Tld,
            domain.Status,
            domain.RegisteredAt,
            domain.ExpiresAt,
            domain.AutoRenew,
            domain.WhoisPrivacy,
            domain.IsLocked,
            domain.RegistrarRef,
            domain.EppCode,
            domain.LinkedServiceId,
            domain.FirstPaymentAmount,
            domain.RecurringAmount,
            domain.PaymentMethod,
            domain.PromotionCode,
            domain.SubscriptionId,
            domain.AdminNotes,
            domain.OrderId,
            domain.OrderType,
            domain.DnsManagement,
            domain.EmailForwarding,
            domain.PriceCurrency,
            domain.NextDueDate,
            domain.Registrar,
            domain.RegistrationPeriod,
            domain.Nameservers
                .Select(n => new NameserverDto(n.Id, n.Host))
                .ToList(),
            domain.DnsRecords
                .Select(r => new DnsRecordDto(r.Id, r.Type, r.Host, r.Value, r.Ttl, r.Priority))
                .ToList(),
            domain.EmailForwardingRules
                .Select(r => new EmailForwardingRuleDto(r.Id, r.Source, r.Destination, r.IsActive))
                .ToList(),
            domain.Reminders
                .Select(r => new DomainReminderDto(r.Id, r.ReminderType, r.SentTo, r.SentAt))
                .ToList());
    }
}
