namespace Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Abstraction over a domain registrar provider (Namecheap, ResellerClub, ENOM, etc.).
/// Implementations live in Infrastructure/Integrations.
/// </summary>
public interface IRegistrarProvider
{
    /// <summary>
    /// Registers a new domain with the registrar.
    /// </summary>
    /// <param name="request">Registration parameters including name, years, and nameservers.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the registrar reference and expiry date on success.</returns>
    Task<RegistrarResult> RegisterAsync(RegisterDomainRequest request, CancellationToken ct);

    /// <summary>
    /// Initiates an incoming domain transfer from another registrar.
    /// </summary>
    /// <param name="request">Transfer parameters including domain name and EPP code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the registrar transfer reference on success.</returns>
    Task<RegistrarResult> TransferAsync(TransferDomainRequest request, CancellationToken ct);

    /// <summary>
    /// Renews an existing domain registration for additional years.
    /// </summary>
    /// <param name="request">Renewal parameters including registrar reference and years.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the updated expiry date on success.</returns>
    Task<RegistrarResult> RenewAsync(RenewDomainRequest request, CancellationToken ct);

    /// <summary>
    /// Cancels a pending incoming transfer before it completes.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">Registrar transfer reference to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the cancellation succeeded.</returns>
    Task<RegistrarResult> CancelTransferAsync(string domainName, string registrarRef, CancellationToken ct);

    /// <summary>
    /// Initiates an outgoing transfer to another registrar (unlocks and generates EPP code).
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success; the EPP code is retrieved separately via <see cref="GetEppCodeAsync"/>.</returns>
    Task<RegistrarResult> InitiateOutgoingTransferAsync(string domainName, string registrarRef, CancellationToken ct);

    /// <summary>
    /// Enables or disables automatic renewal at the registrar level.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="enabled">Whether auto-renew should be enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    Task<RegistrarResult> SetAutoRenewAsync(string domainName, string registrarRef, bool enabled, CancellationToken ct);

    /// <summary>
    /// Enables or disables WHOIS privacy for a domain.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="enabled">Whether WHOIS privacy should be enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    Task<RegistrarResult> SetWhoisPrivacyAsync(string domainName, string registrarRef, bool enabled, CancellationToken ct);

    /// <summary>
    /// Enables or disables the registrar lock (transfer-prohibit status) for a domain.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="locked">Whether the domain should be locked.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the lock status was updated.</returns>
    Task<RegistrarResult> SetRegistrarLockAsync(string domainName, string registrarRef, bool locked, CancellationToken ct);

    /// <summary>
    /// Retrieves the EPP (authorization) code for an outgoing transfer.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The EPP code, or null if the registrar does not support retrieval.</returns>
    Task<string?> GetEppCodeAsync(string domainName, string registrarRef, CancellationToken ct);

    /// <summary>
    /// Updates the nameservers for a domain at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="nameservers">New list of nameserver hostnames (minimum 2).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the nameservers were updated.</returns>
    Task<RegistrarResult> SetNameserversAsync(string domainName, string registrarRef, IReadOnlyList<string> nameservers, CancellationToken ct);

    /// <summary>
    /// Retrieves all DNS records for a domain from the registrar's DNS zone.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of DNS records in the zone.</returns>
    Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(string domainName, string registrarRef, CancellationToken ct);

    /// <summary>
    /// Adds a new DNS record to the domain's zone at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="record">The DNS record to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the record was added.</returns>
    Task<RegistrarResult> AddDnsRecordAsync(string domainName, string registrarRef, DnsRecord record, CancellationToken ct);

    /// <summary>
    /// Updates an existing DNS record in the domain's zone at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="record">The updated DNS record (matched by ID or host+type).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the record was updated.</returns>
    Task<RegistrarResult> UpdateDnsRecordAsync(string domainName, string registrarRef, DnsRecord record, CancellationToken ct);

    /// <summary>
    /// Deletes a DNS record from the domain's zone at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="recordId">The ID of the DNS record to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the record was deleted.</returns>
    Task<RegistrarResult> DeleteDnsRecordAsync(string domainName, string registrarRef, int recordId, CancellationToken ct);

    /// <summary>
    /// Modifies the WHOIS registrant contact details for a domain at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="contact">The updated contact details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the contact details were updated.</returns>
    Task<RegistrarResult> ModifyContactDetailsAsync(string domainName, DomainContact contact, CancellationToken ct);

    /// <summary>
    /// Enables or disables email forwarding for a domain at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="enabled">Whether email forwarding should be enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    Task<RegistrarResult> SetEmailForwardingAsync(string domainName, bool enabled, CancellationToken ct);

    /// <summary>
    /// Adds an email forwarding rule for a domain at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="source">Source alias or local part (e.g. "info").</param>
    /// <param name="destination">Destination email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the rule was added.</returns>
    Task<RegistrarResult> AddEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct);

    /// <summary>
    /// Updates an existing email forwarding rule for a domain at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="source">Source alias or local part to update.</param>
    /// <param name="destination">New destination email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the rule was updated.</returns>
    Task<RegistrarResult> UpdateEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct);

    /// <summary>
    /// Deletes an email forwarding rule for a domain at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="source">Source alias or local part to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the rule was deleted.</returns>
    Task<RegistrarResult> DeleteEmailForwardingRuleAsync(string domainName, string source, CancellationToken ct);

    /// <summary>
    /// Enables or disables DNS management for a domain at the registrar.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="enabled">Whether DNS management should be enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the update succeeded.</returns>
    Task<RegistrarResult> SetDnsManagementAsync(string domainName, bool enabled, CancellationToken ct);

    /// <summary>
    /// Checks whether a domain name is available for registration.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the domain is available; otherwise <see langword="false"/>.</returns>
    Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct);

    /// <summary>
    /// Performs a WHOIS lookup for the given domain name.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WHOIS information, or <see langword="null"/> if the domain is not found.</returns>
    Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct);
}
