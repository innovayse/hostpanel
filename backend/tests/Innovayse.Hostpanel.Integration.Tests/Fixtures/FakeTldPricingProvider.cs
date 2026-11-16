using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IRegistrarProvider"/> standing in for a real registrar in the TLD import and
/// sync tests: every member but <see cref="GetTldPricingAsync"/> answers a trivial success that carries no
/// date (no test here reads one, and a date would need a clock this fake has no host for), and the pricing catalogue a test sees is whatever it assigned to
/// <see cref="PricingToReturn"/> beforehand. A fake port, not a mock of our own code: the real
/// registrar is an upstream HTTP service this backend does not own.
/// </summary>
public sealed class FakeTldPricingProvider : IRegistrarProvider
{
    /// <summary>The catalogue <see cref="GetTldPricingAsync"/> answers with. Empty until a test sets it.</summary>
    public IReadOnlyList<TldPricing> PricingToReturn { get; set; } = [];

    /// <summary>How many times <see cref="GetTldPricingAsync"/> was called.</summary>
    public int PricingCallCount { get; private set; }

    /// <inheritdoc/>
    public Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-TRANSFER-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-RENEW-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult<string?>("FAKE-EPP");

    /// <inheritdoc/>
    public Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DnsRecord>>([]);

    /// <inheritdoc/>
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, removal.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<DomainContact?> GetContactDetailsAsync(string domainName, CancellationToken ct) =>
        Task.FromResult<DomainContact?>(null);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct) => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<RegistrarResult> CheckDomainActiveAsync(string domainName, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, domainName, null, null));

    /// <inheritdoc/>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct) => Task.FromResult<WhoisInfo?>(null);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TldPricing>> GetTldPricingAsync(CancellationToken ct)
    {
        PricingCallCount++;
        return Task.FromResult(PricingToReturn);
    }
}
