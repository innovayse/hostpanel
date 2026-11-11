using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Abstraction over a domain registrar provider (any registrar API supplier).
/// Implementations live in Infrastructure/Integrations.
/// </summary>
/// <remarks>
/// A purchase or a change succeeds only when the registrar did it. An unconfigured registrar, or
/// one that refuses, answers with a failed <see cref="RegistrarResult"/>, never a no-op success;
/// an exception means the outcome is unknown (a timeout, a 5xx).
/// </remarks>
public interface IRegistrarProvider
{
    /// <summary>
    /// Registers a new domain with the registrar.
    /// </summary>
    /// <param name="request">Registration parameters including name, years, and nameservers.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, with a non-empty registrar reference and the expiry, only when the registrar
    /// accepted the registration; a failure when it refused or is not configured.
    /// </returns>
    public Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct);

    /// <summary>
    /// Initiates an incoming domain transfer from another registrar.
    /// </summary>
    /// <param name="request">Transfer parameters including domain name and EPP code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, with a non-empty transfer reference, only when the registrar accepted the
    /// transfer; a failure when it refused or is not configured.
    /// </returns>
    public Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct);

    /// <summary>
    /// Renews an existing domain registration for additional years.
    /// </summary>
    /// <param name="request">Renewal parameters including registrar reference and years.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success only when the registrar renewed the domain, with the new expiry when the registrar
    /// reports one (otherwise <see langword="null"/>, and the caller extends the current expiry).
    /// </returns>
    public Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct);

    /// <summary>
    /// Cancels a pending incoming transfer before it completes.
    /// </summary>
    /// <param name="domain">The domain at the registrar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the cancellation succeeded.</returns>
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct);

    /// <summary>
    /// Initiates an outgoing transfer to another registrar (unlocks and generates EPP code).
    /// </summary>
    /// <param name="domain">The domain at the registrar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success; the EPP code is retrieved separately via <see cref="GetEppCodeAsync"/>.</returns>
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct);

    /// <summary>
    /// Enables or disables automatic renewal at the registrar level.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    public Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct);

    /// <summary>
    /// Enables or disables WHOIS privacy for a domain.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    public Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct);

    /// <summary>
    /// Enables or disables the registrar lock (transfer-prohibit status) for a domain.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the lock status was updated.</returns>
    public Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct);

    /// <summary>
    /// Retrieves the EPP (authorization) code for an outgoing transfer.
    /// </summary>
    /// <param name="domain">The domain at the registrar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The EPP code, or null if the registrar does not support retrieval.</returns>
    public Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct);

    /// <summary>
    /// Updates the nameservers for a domain at the registrar.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the nameservers were updated.</returns>
    public Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct);

    /// <summary>
    /// Retrieves all DNS records for a domain from the registrar's DNS zone.
    /// </summary>
    /// <param name="domain">The domain at the registrar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of DNS records in the zone.</returns>
    public Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct);

    /// <summary>
    /// Adds a new DNS record to the domain's zone at the registrar.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the record was added.</returns>
    public Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct);

    /// <summary>
    /// Updates an existing DNS record in the domain's zone at the registrar.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the record was updated.</returns>
    public Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordChange change, CancellationToken ct);

    /// <summary>
    /// Deletes a DNS record from the domain's zone at the registrar.
    /// </summary>
    /// <param name="removal">What to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the record was deleted.</returns>
    public Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct);

    /// <summary>
    /// Modifies the WHOIS registrant contact details for a domain at the registrar.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the contact details were updated.</returns>
    public Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct);

    /// <summary>
    /// Reads the current WHOIS registrant contact details of a domain from the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The registrant contact the registrar holds, or <see langword="null"/> when the registrar
    /// has no contact record for the name or is not configured.
    /// </returns>
    public Task<DomainContact?> GetContactDetailsAsync(string domainName, CancellationToken ct);

    /// <summary>
    /// Enables or disables email forwarding for a domain at the registrar.
    /// </summary>
    /// <param name="toggle">The feature and whether it should be on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct);

    /// <summary>
    /// Adds an email forwarding rule for a domain at the registrar.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the rule was added.</returns>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct);

    /// <summary>
    /// Updates an existing email forwarding rule for a domain at the registrar.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the rule was updated.</returns>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct);

    /// <summary>
    /// Deletes an email forwarding rule for a domain at the registrar.
    /// </summary>
    /// <param name="removal">What to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the rule was deleted.</returns>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct);

    /// <summary>
    /// Enables or disables DNS management for a domain at the registrar.
    /// </summary>
    /// <param name="toggle">The feature and whether it should be on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct);

    /// <summary>
    /// Checks whether a domain name is available for registration.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the domain is available; otherwise <see langword="false"/>.</returns>
    public Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct);

    /// <summary>
    /// Polls the registrar to check whether a pending domain registration or transfer has become active.
    /// Used by the sync job for registrars that require polling (see <see cref="RegistrarResult.RequiresPolling"/>).
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="RegistrarResult"/> with <see cref="RegistrarResult.Success"/> set to <see langword="true"/>
    /// and a valid <see cref="RegistrarResult.RegistrarRef"/> and <see cref="RegistrarResult.ExpiresAt"/> when the
    /// domain is now active at the registrar; otherwise <see cref="RegistrarResult.Success"/> is <see langword="false"/>.
    /// </returns>
    public Task<RegistrarResult> CheckDomainActiveAsync(string domainName, CancellationToken ct);

    /// <summary>
    /// Performs a WHOIS lookup for the given domain name.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WHOIS information, or <see langword="null"/> if the domain is not found.</returns>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct);

    /// <summary>
    /// Retrieves TLD pricing from the registrar's product catalogue.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A list of <see cref="TldPricing"/> entries, one per TLD, containing register/renew/transfer
    /// prices keyed by period in years.
    /// </returns>
    public Task<IReadOnlyList<TldPricing>> GetTldPricingAsync(CancellationToken ct);
}
