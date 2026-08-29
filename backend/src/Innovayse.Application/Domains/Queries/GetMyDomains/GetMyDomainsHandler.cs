namespace Innovayse.Application.Domains.Queries.GetMyDomains;

using System.Linq;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Application.Domains.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>Returns every domain owned by the calling client as full <see cref="DomainDto"/> items.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="clientRepo">Resolves the caller's client record.</param>
/// <param name="caller">Who is asking; the query does not say, and must not.</param>
public sealed class GetMyDomainsHandler(
    IDomainRepository repo,
    IClientRepository clientRepo,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Handles <see cref="GetMyDomainsQuery"/>.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All domains for the client, including nameservers and DNS records.</returns>
    /// <exception cref="ClientProfileNotFoundException">
    /// Thrown when no client record exists for the user. Carries the user id for the log only --
    /// the API answers 404 with a code and an identifier-free sentence.
    /// </exception>
    public async Task<IReadOnlyList<DomainDto>> HandleAsync(GetMyDomainsQuery query, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

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
