namespace Innovayse.Application.Domains.Queries.GetMyDomains;

using System.Linq;
using Innovayse.Application.Domains.DTOs;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>Returns all domains owned by the authenticated client as full <see cref="DomainDto"/> items.</summary>
public sealed class GetMyDomainsHandler(IDomainRepository repo, IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="GetMyDomainsQuery"/>.
    /// </summary>
    /// <param name="query">The get my domains query containing the authenticated user's Identity ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All domains for the client, including nameservers and DNS records.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client record exists for the user.</exception>
    public async Task<IReadOnlyList<DomainDto>> HandleAsync(GetMyDomainsQuery query, CancellationToken ct)
    {
        var client = await clientRepo.FindByUserIdAsync(query.UserId, ct)
            ?? throw new InvalidOperationException($"No client profile found for user {query.UserId}.");

        var domains = await repo.ListByClientAsync(client.Id, ct);

        return domains
            .Select(d => new DomainDto(
                d.Id,
                d.ClientId,
                d.Name,
                d.Tld,
                d.Status,
                d.RegisteredAt,
                d.ExpiresAt,
                d.AutoRenew,
                d.WhoisPrivacy,
                d.IsLocked,
                d.RegistrarRef,
                d.EppCode,
                d.LinkedServiceId,
                d.FirstPaymentAmount,
                d.RecurringAmount,
                d.PaymentMethod,
                d.PromotionCode,
                d.SubscriptionId,
                d.AdminNotes,
                d.OrderId,
                d.OrderType,
                d.DnsManagement,
                d.EmailForwarding,
                d.PriceCurrency,
                d.NextDueDate,
                d.Registrar,
                d.RegistrationPeriod,
                d.Nameservers.Select(n => new NameserverDto(n.Id, n.Host)).ToList(),
                d.DnsRecords.Select(r => new DnsRecordDto(r.Id, r.Type, r.Host, r.Value, r.Ttl, r.Priority)).ToList(),
                d.EmailForwardingRules.Select(r => new EmailForwardingRuleDto(r.Id, r.Source, r.Destination, r.IsActive)).ToList(),
                d.Reminders.Select(r => new DomainReminderDto(r.Id, r.ReminderType, r.SentTo, r.SentAt)).ToList()))
            .ToList();
    }
}
