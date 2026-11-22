using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Domain registrar for the public demo: every operation succeeds at once, deterministically, with
/// no network. References are derived from the domain name, so the same name always gets the same
/// reference, and every name is available except the ones that spell <c>taken</c>.
/// </summary>
public sealed class DemoRegistrarProvider : IRegistrarProvider
{
    /// <summary>A name containing this word reads as already registered, so the demo can show both answers.</summary>
    private const string TakenMarker = "taken";

    /// <summary>The registration term, in years, the fake registry grants on every order.</summary>
    private const int TermYears = 1;

    /// <summary>The sample contact the fake registry returns for any domain.</summary>
    private static readonly DomainContact SampleContact = new(
        "Demo", "Registrant", "Demo Registrant LLC", "registrant@demo.example", "+1 555 0100",
        "1 Demo Street", null, "Northvale", "Northvale", "00001", "US");

    /// <summary>The source of the current moment the fake registry dates its answers from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the fake registrar.</summary>
    /// <param name="clock">The source of the current moment the fake registry dates its answers from.</param>
    public DemoRegistrarProvider(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>Builds the reference the registry would hand back for a name.</summary>
    /// <param name="domainName">The domain name.</param>
    /// <returns>A stable reference.</returns>
    private static string RefFor(string domainName) => $"DEMO-{domainName.Trim().ToLowerInvariant()}";

    /// <summary>A successful result that carries the reference and, for orders, the new expiry.</summary>
    /// <param name="registrarRef">The registry reference.</param>
    /// <param name="withExpiry">Whether the result carries an expiry one term from now.</param>
    /// <returns>The success result.</returns>
    private Task<RegistrarResult> Ok(string registrarRef, bool withExpiry = false) =>
        Task.FromResult(new RegistrarResult(
            true, registrarRef, withExpiry ? _clock.UtcNow.AddYears(TermYears) : null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct) =>
        Ok(RefFor(request.DomainName), true);

    /// <inheritdoc/>
    public Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct) =>
        Ok(RefFor(request.DomainName), true);

    /// <inheritdoc/>
    public Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct) =>
        Ok(request.RegistrarRef, true);

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct) =>
        Ok(domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct) =>
        Ok(domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct) =>
        Ok(change.Domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct) =>
        Ok(change.Domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct) =>
        Ok(change.Domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult<string?>("DEMO-EPP-CODE");

    /// <inheritdoc/>
    public Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct) =>
        Ok(change.Domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<DnsRecord>>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct)
    {
        IReadOnlyList<DnsRecord> records =
        [
            DnsRecord.CreateDetached(DnsRecordType.A, "@", "192.0.2.10", 3600, null),
            DnsRecord.CreateDetached(DnsRecordType.CNAME, "www", domain.Name, 3600, null),
            DnsRecord.CreateDetached(DnsRecordType.MX, "@", $"mail.{domain.Name}", 3600, 10),
        ];

        return Task.FromResult(Result<IReadOnlyList<DnsRecord>>.Success(records));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct) =>
        Ok(change.Domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordUpdate update, CancellationToken ct) =>
        Ok(update.Domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct) =>
        Ok(removal.Domain.RegistrarRef);

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct) =>
        Ok(RefFor(change.Domain.Name));

    /// <inheritdoc/>
    public Task<DomainContact?> GetContactDetailsAsync(DomainRef domain, CancellationToken ct) =>
        Task.FromResult<DomainContact?>(SampleContact);

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct) =>
        Ok(RefFor(toggle.Domain.Name));

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct) =>
        Ok(RefFor(change.Domain.Name));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct) =>
        Ok(RefFor(change.Domain.Name));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct) =>
        Ok(RefFor(removal.Domain.Name));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct) =>
        Ok(RefFor(toggle.Domain.Name));

    /// <inheritdoc/>
    public Task<Result<bool>> CheckAvailabilityAsync(string domainName, CancellationToken ct) =>
        Task.FromResult(Result<bool>.Success(!domainName.Contains(TakenMarker, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc/>
    public Task<RegistrarResult> CheckDomainActiveAsync(DomainRef domain, CancellationToken ct) =>
        Ok(RefFor(domain.Name), true);

    /// <inheritdoc/>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        var created = _clock.UtcNow.AddMonths(-3);
        return Task.FromResult<WhoisInfo?>(new WhoisInfo("Demo Registrar", "Demo Registrant LLC", created, created.AddYears(TermYears)));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<TldPricing>>> GetTldPricingAsync(CancellationToken ct)
    {
        IReadOnlyList<TldPricing> pricing =
        [
            new TldPricing(
                "com", "USD",
                new Dictionary<int, decimal> { [1] = 9.6m },
                new Dictionary<int, decimal> { [1] = 9.6m },
                new Dictionary<int, decimal> { [1] = 12m }),
        ];

        return Task.FromResult(Result<IReadOnlyList<TldPricing>>.Success(pricing));
    }
}
