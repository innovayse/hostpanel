using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The test registrars' common shape: behaves like <see cref="StubRegistrarProvider"/> in every
/// member but <see cref="RenewAsync"/>, which each variant defines (a variant may also override
/// <see cref="RegisterAsync"/>), and counts renewal calls per
/// domain name. Top-level and public, not nested and private: Wolverine's generated handler code
/// must be able to name the type it resolves for <c>RenewDomainHandler</c>. The counter is static
/// because the registrar is scoped and a fresh instance serves every handler.
/// </summary>
public abstract class TestRegistrar : IRegistrarProvider
{
    /// <summary>Renewal calls per domain name, across every instance.</summary>
    private static readonly ConcurrentDictionary<string, int> RenewCalls = new();

    /// <summary>The stub every non-renewal call goes to.</summary>
    private readonly StubRegistrarProvider _inner = new();

    /// <summary>How many times any test registrar was asked to renew <paramref name="domainName"/>.</summary>
    /// <param name="domainName">The domain name.</param>
    /// <returns>The call count.</returns>
    public static int RenewCallsFor(string domainName) => RenewCalls.GetValueOrDefault(domainName);

    /// <inheritdoc/>
    public Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct)
    {
        RenewCalls.AddOrUpdate(request.DomainName, 1, (_, n) => n + 1);
        return Renew(request, _inner, ct);
    }

    /// <summary>The variant's answer to a renewal.</summary>
    /// <param name="request">The renewal order.</param>
    /// <param name="stub">The succeeding stub, for variants that delegate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The registrar's answer.</returns>
    protected abstract Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct);

    /// <inheritdoc/>
    public virtual Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct) => _inner.RegisterAsync(request, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct) => _inner.TransferAsync(request, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct) =>
        _inner.CancelTransferAsync(domain, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct) =>
        _inner.InitiateOutgoingTransferAsync(domain, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct) =>
        _inner.SetAutoRenewAsync(change, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct) =>
        _inner.SetWhoisPrivacyAsync(change, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct) =>
        _inner.SetRegistrarLockAsync(change, ct);

    /// <inheritdoc/>
    public Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct) =>
        _inner.GetEppCodeAsync(domain, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct) =>
        _inner.SetNameserversAsync(change, ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct) =>
        _inner.GetDnsRecordsAsync(domain, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct) =>
        _inner.AddDnsRecordAsync(change, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordChange change, CancellationToken ct) =>
        _inner.UpdateDnsRecordAsync(change, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct) =>
        _inner.DeleteDnsRecordAsync(removal, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct) =>
        _inner.ModifyContactDetailsAsync(change, ct);

    /// <inheritdoc/>
    public Task<DomainContact?> GetContactDetailsAsync(string domainName, CancellationToken ct) =>
        _inner.GetContactDetailsAsync(domainName, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct) =>
        _inner.SetEmailForwardingAsync(toggle, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct) =>
        _inner.AddEmailForwardingRuleAsync(change, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct) =>
        _inner.UpdateEmailForwardingRuleAsync(change, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct) =>
        _inner.DeleteEmailForwardingRuleAsync(removal, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct) =>
        _inner.SetDnsManagementAsync(toggle, ct);

    /// <inheritdoc/>
    public Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct) => _inner.CheckAvailabilityAsync(domainName, ct);

    /// <inheritdoc/>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct) => _inner.GetWhoisAsync(domainName, ct);

    /// <inheritdoc/>
    public Task<RegistrarResult> CheckDomainActiveAsync(string domainName, CancellationToken ct) =>
        _inner.CheckDomainActiveAsync(domainName, ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TldPricing>> GetTldPricingAsync(CancellationToken ct) => _inner.GetTldPricingAsync(ct);
}
