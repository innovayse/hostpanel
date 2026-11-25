using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IRegistrarProvider"/> that answers like <see cref="StubRegistrarProvider"/> and
/// remembers which operation it was asked to perform for which domain. A test registers two of
/// them behind a <see cref="FakeRegistrarProviderFactory"/> to prove each domain is handled by the
/// registrar its TLD names, and by no other. Top-level and internal so Wolverine's generated
/// handler code can name every type it resolves.
/// </summary>
internal sealed class RecordingRegistrar : IRegistrarProvider
{
    /// <summary>The stub every operation is answered by.</summary>
    private readonly StubRegistrarProvider _inner;

    /// <summary>Initializes a new instance of the <see cref="RecordingRegistrar"/> class.</summary>
    /// <param name="time">The clock the stub it delegates to reports its dates from.</param>
    public RecordingRegistrar(TimeProvider time)
    {
        _inner = new StubRegistrarProvider(time);
    }

    /// <summary>Every operation this registrar was asked for, as "Operation domain", in call order.</summary>
    private readonly ConcurrentQueue<string> _calls = new();

    /// <summary>Gets the answer to a transfer cancellation when a test scripts one; otherwise the stub's success answers.</summary>
    public RegistrarResult? CancelTransferAnswer { get; init; }

    /// <summary>Gets the answer to an auto renew change when a test scripts one; otherwise the stub's success answers.</summary>
    public RegistrarResult? SetAutoRenewAnswer { get; init; }

    /// <summary>Gets a snapshot of the operations this registrar was asked for, as "Operation domain".</summary>
    public IReadOnlyList<string> Calls => [.. _calls];

    /// <summary>Notes one operation against a domain.</summary>
    /// <param name="operation">The port member name.</param>
    /// <param name="domainName">The domain the operation concerns.</param>
    private void Note(string operation, string domainName)
    {
        _calls.Enqueue($"{operation} {domainName}");
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
    {
        Note(nameof(RegisterAsync), request.DomainName);
        return _inner.RegisterAsync(request, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct)
    {
        Note(nameof(TransferAsync), request.DomainName);
        return _inner.TransferAsync(request, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct)
    {
        Note(nameof(RenewAsync), request.DomainName);
        return _inner.RenewAsync(request, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct)
    {
        Note(nameof(CancelTransferAsync), domain.Name);
        return CancelTransferAnswer is { } answer ? Task.FromResult(answer) : _inner.CancelTransferAsync(domain, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct)
    {
        Note(nameof(InitiateOutgoingTransferAsync), domain.Name);
        return _inner.InitiateOutgoingTransferAsync(domain, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        Note(nameof(SetAutoRenewAsync), change.Domain.Name);
        return SetAutoRenewAnswer is { } answer ? Task.FromResult(answer) : _inner.SetAutoRenewAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        Note(nameof(SetWhoisPrivacyAsync), change.Domain.Name);
        return _inner.SetWhoisPrivacyAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        Note(nameof(SetRegistrarLockAsync), change.Domain.Name);
        return _inner.SetRegistrarLockAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct)
    {
        Note(nameof(GetEppCodeAsync), domain.Name);
        return _inner.GetEppCodeAsync(domain, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct)
    {
        Note(nameof(SetNameserversAsync), change.Domain.Name);
        return _inner.SetNameserversAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<DnsRecord>>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct)
    {
        Note(nameof(GetDnsRecordsAsync), domain.Name);
        return _inner.GetDnsRecordsAsync(domain, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct)
    {
        Note(nameof(AddDnsRecordAsync), change.Domain.Name);
        return _inner.AddDnsRecordAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordUpdate update, CancellationToken ct)
    {
        Note(nameof(UpdateDnsRecordAsync), update.Domain.Name);
        return _inner.UpdateDnsRecordAsync(update, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct)
    {
        Note(nameof(DeleteDnsRecordAsync), removal.Domain.Name);
        return _inner.DeleteDnsRecordAsync(removal, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct)
    {
        Note(nameof(ModifyContactDetailsAsync), change.Domain.Name);
        return _inner.ModifyContactDetailsAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<DomainContact?> GetContactDetailsAsync(DomainRef domain, CancellationToken ct)
    {
        Note(nameof(GetContactDetailsAsync), domain.Name);
        return _inner.GetContactDetailsAsync(domain, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        Note(nameof(SetEmailForwardingAsync), toggle.Domain.Name);
        return _inner.SetEmailForwardingAsync(toggle, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        Note(nameof(AddEmailForwardingRuleAsync), change.Domain.Name);
        return _inner.AddEmailForwardingRuleAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        Note(nameof(UpdateEmailForwardingRuleAsync), change.Domain.Name);
        return _inner.UpdateEmailForwardingRuleAsync(change, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct)
    {
        Note(nameof(DeleteEmailForwardingRuleAsync), removal.Domain.Name);
        return _inner.DeleteEmailForwardingRuleAsync(removal, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        Note(nameof(SetDnsManagementAsync), toggle.Domain.Name);
        return _inner.SetDnsManagementAsync(toggle, ct);
    }

    /// <inheritdoc/>
    public Task<Result<bool>> CheckAvailabilityAsync(string domainName, CancellationToken ct)
    {
        Note(nameof(CheckAvailabilityAsync), domainName);
        return _inner.CheckAvailabilityAsync(domainName, ct);
    }

    /// <inheritdoc/>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        Note(nameof(GetWhoisAsync), domainName);
        return _inner.GetWhoisAsync(domainName, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> CheckDomainActiveAsync(DomainRef domain, CancellationToken ct)
    {
        Note(nameof(CheckDomainActiveAsync), domain.Name);
        return _inner.CheckDomainActiveAsync(domain, ct);
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<TldPricing>>> GetTldPricingAsync(CancellationToken ct)
    {
        return _inner.GetTldPricingAsync(ct);
    }
}
