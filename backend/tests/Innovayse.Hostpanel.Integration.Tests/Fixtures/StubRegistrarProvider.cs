using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Test stub for <see cref="IRegistrarProvider"/>.
/// Returns hardcoded success results without making any real network calls.
/// </summary>
/// <remarks>
/// The expiry it reports is a year from the moment its host's clock reads: on a clocked host (<see cref="ClockedHost"/>, standing at
/// <see cref="TestMoments.Seeded"/> unless the test says otherwise) that is the expiry of the domains the tests seed relative to the
/// clock, and on the shared host, which runs on the system clock, it is a year from the real present.
/// </remarks>
public sealed class StubRegistrarProvider : IRegistrarProvider
{
    /// <summary>The clock of the host the stub answers for.</summary>
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="StubRegistrarProvider"/> class.</summary>
    /// <param name="time">The clock of the host the stub answers for.</param>
    public StubRegistrarProvider(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Every name <see cref="GetWhoisAsync"/> was asked about, in call order; the stub is scoped, so the record is static.</summary>
    public static readonly System.Collections.Concurrent.ConcurrentQueue<string> WhoisLookups = new();

    /// <inheritdoc/>
    public Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF-123", _time.GetUtcNow().AddYears(1), null));

    /// <inheritdoc/>
    public Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-TRANSFER-456", _time.GetUtcNow().AddYears(1), null));

    /// <inheritdoc/>
    /// <remarks>A domain seeded relative to the host's clock expires a year after it, so a renewal reports that year plus the years bought.</remarks>
    public Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-RENEW-789", _time.GetUtcNow().AddYears(1 + request.Years), null));

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct)
        => Task.FromResult<string?>("STUB-EPP-CODE");

    /// <inheritdoc/>
    public Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<DnsRecord>>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct)
        => Task.FromResult(Result<IReadOnlyList<DnsRecord>>.Success([]));

    /// <inheritdoc/>
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, change.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordUpdate update, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, update.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, removal.Domain.RegistrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <summary>The TLD suffix under which the stub models a name the registrar holds no contact for.</summary>
    public const string NoContactSuffix = ".test";

    /// <summary>The registrant contact the stub answers with for every name outside <see cref="NoContactSuffix"/>.</summary>
    public static readonly DomainContact StubContact = new(
        "Stub", "Registrant", "Stub Org", "registrant@example.test", "+15550100",
        "1 Registry St", null, "Springfield", "Springfield", "00100", "US");

    /// <inheritdoc/>
    public Task<DomainContact?> GetContactDetailsAsync(DomainRef domain, CancellationToken ct)
        => Task.FromResult<DomainContact?>(
            domain.Name.EndsWith(NoContactSuffix, StringComparison.OrdinalIgnoreCase) ? null : StubContact);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<Result<bool>> CheckAvailabilityAsync(string domainName, CancellationToken ct)
        => Task.FromResult(Result<bool>.Success(true));

    /// <inheritdoc/>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        WhoisLookups.Enqueue(domainName);
        return Task.FromResult<WhoisInfo?>(null);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> CheckDomainActiveAsync(DomainRef domain, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, domain.Name, _time.GetUtcNow().AddYears(1), null));

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<TldPricing>>> GetTldPricingAsync(CancellationToken ct)
        => Task.FromResult(Result<IReadOnlyList<TldPricing>>.Success([]));
}
