using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IRegistrarProvider"/> standing in for a real registrar in the TLD import, sync and
/// availability tests: every member but <see cref="GetTldPricingAsync"/> answers a trivial success that carries no
/// date (no test here reads one, and a date would need a clock this fake has no host for), and the pricing catalogue a test sees is whatever it assigned to
/// <see cref="PricingToReturn"/> beforehand. A fake port, not a mock of our own code: the real
/// registrar is an upstream HTTP service this backend does not own.
/// </summary>
public sealed class FakeTldPricingProvider : IRegistrarProvider
{
    /// <summary>The catalogue <see cref="GetTldPricingAsync"/> answers with. Empty until a test sets it.</summary>
    public IReadOnlyList<TldPricing> PricingToReturn { get; set; } = [];

    /// <summary>The <c>REGISTRAR_*</c> code <see cref="GetTldPricingAsync"/> refuses with, or <see langword="null"/> to answer the catalogue.</summary>
    public string? PricingFailure { get; set; }

    /// <summary>The <c>REGISTRAR_*</c> code <see cref="CheckAvailabilityAsync"/> refuses with, or <see langword="null"/> to answer "available".</summary>
    public string? AvailabilityFailure { get; set; }

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
    public Task<Result<IReadOnlyList<DnsRecord>>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<DnsRecord>>.Success([]));

    /// <inheritdoc/>
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordUpdate update, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, update.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, removal.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, "FAKE-REF", null, null));

    /// <inheritdoc/>
    public Task<DomainContact?> GetContactDetailsAsync(DomainRef domain, CancellationToken ct) =>
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
    public Task<Result<bool>> CheckAvailabilityAsync(string domainName, CancellationToken ct) =>
        Task.FromResult(AvailabilityFailure is null
            ? Result<bool>.Success(true)
            : Result<bool>.FailureCode(AvailabilityFailure));

    /// <inheritdoc/>
    public Task<RegistrarResult> CheckDomainActiveAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult(new RegistrarResult(true, domain.Name, null, null));

    /// <inheritdoc/>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct) => Task.FromResult<WhoisInfo?>(null);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<TldPricing>>> GetTldPricingAsync(CancellationToken ct)
    {
        PricingCallCount++;
        return Task.FromResult(PricingFailure is null
            ? Result<IReadOnlyList<TldPricing>>.Success(PricingToReturn)
            : Result<IReadOnlyList<TldPricing>>.FailureCode(PricingFailure));
    }
}
