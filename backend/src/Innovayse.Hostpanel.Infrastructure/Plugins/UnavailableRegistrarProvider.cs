using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The provider a module without a usable registrar plugin resolves to: none installed, none switched on, a required setting
/// missing, a data migration pending, or no module configured for the name. It refuses every operation the way an
/// unconfigured registrar always has, with <c>REGISTRAR_NOT_CONFIGURED</c>, so a caller needs no separate branch.
/// </summary>
public sealed class UnavailableRegistrarProvider : IRegistrarProvider
{
    /// <inheritdoc />
    public Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct)
    {
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<DnsRecord>>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct)
    {
        return Task.FromResult(Result<IReadOnlyList<DnsRecord>>.Success([]));
    }

    /// <inheritdoc />
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordUpdate update, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<DomainContact?> GetContactDetailsAsync(DomainRef domain, CancellationToken ct)
    {
        return Task.FromResult<DomainContact?>(null);
    }

    /// <inheritdoc />
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always <c>REGISTRAR_NOT_CONFIGURED</c>: with no registrar to ask, "available" is unknown, and a name must never be
    /// reported free or taken on a guess.
    /// </remarks>
    public Task<Result<bool>> CheckAvailabilityAsync(string domainName, CancellationToken ct)
    {
        return Task.FromResult(Result<bool>.FailureCode(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED));
    }

    /// <inheritdoc />
    public Task<RegistrarResult> CheckDomainActiveAsync(DomainRef domain, CancellationToken ct)
    {
        return Task.FromResult(Refused());
    }

    /// <inheritdoc />
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        return Task.FromResult<WhoisInfo?>(null);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<TldPricing>>> GetTldPricingAsync(CancellationToken ct)
    {
        return Task.FromResult(Result<IReadOnlyList<TldPricing>>.Success([]));
    }

    /// <inheritdoc />
    /// <remarks>Always <c>REGISTRAR_NOT_CONFIGURED</c>: with no registrar to ask, nothing is held.</remarks>
    public Task<Result<RegistrarHeldDomainPage>> ListDomainsAsync(string? cursor, CancellationToken ct)
    {
        return Task.FromResult(Result<RegistrarHeldDomainPage>.FailureCode(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED));
    }

    /// <summary>The answer every operation gets.</summary>
    /// <returns>A failed result carrying <c>REGISTRAR_NOT_CONFIGURED</c>.</returns>
    private static RegistrarResult Refused()
    {
        return new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
    }
}
