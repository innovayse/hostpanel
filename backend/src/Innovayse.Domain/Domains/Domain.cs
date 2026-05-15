namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;
using Innovayse.Domain.Domains.Events;

/// <summary>
/// Aggregate root representing a domain name registration owned by a client.
/// Owns collections of <see cref="Nameserver"/> and <see cref="DnsRecord"/> entities.
/// Stored in the <c>domains</c> table.
/// </summary>
public sealed class Domain : AggregateRoot
{
    /// <summary>Internal mutable list of nameservers assigned to this domain.</summary>
    private readonly List<Nameserver> _nameservers = [];

    /// <summary>Internal mutable list of DNS records in this domain's zone.</summary>
    private readonly List<DnsRecord> _dnsRecords = [];

    /// <summary>Internal mutable list of email forwarding rules for this domain.</summary>
    private readonly List<EmailForwardingRule> _emailForwardingRules = [];

    /// <summary>Internal mutable list of expiry reminders sent for this domain.</summary>
    private readonly List<DomainReminder> _reminders = [];

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the fully-qualified domain name (e.g. "example.com").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the top-level domain suffix extracted from <see cref="Name"/> (e.g. "com").</summary>
    public string Tld { get; private set; } = string.Empty;

    /// <summary>Gets the current lifecycle status.</summary>
    public DomainStatus Status { get; private set; }

    /// <summary>Gets the UTC date the domain was first registered with the registrar.</summary>
    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>Gets the UTC date the current registration period expires.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets whether automatic renewal is enabled for this domain.</summary>
    public bool AutoRenew { get; private set; }

    /// <summary>Gets whether WHOIS privacy is enabled for this domain.</summary>
    public bool WhoisPrivacy { get; private set; }

    /// <summary>Gets whether the domain has the registrar transfer-lock enabled.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>Gets the registrar-assigned reference/order ID; null until activated.</summary>
    public string? RegistrarRef { get; private set; }

    /// <summary>Gets the EPP (authorization) code used for outgoing transfers; null until set.</summary>
    public string? EppCode { get; private set; }

    /// <summary>Gets the FK to a linked hosting service, if any.</summary>
    public int? LinkedServiceId { get; private set; }

    /// <summary>Gets the recurring registration price.</summary>
    public decimal RecurringAmount { get; private set; }

    /// <summary>Gets the ISO 4217 currency code for the price (e.g. "USD", "AMD").</summary>
    public string PriceCurrency { get; private set; } = "USD";

    /// <summary>Gets the next renewal payment due date (UTC).</summary>
    public DateTimeOffset NextDueDate { get; private set; }

    /// <summary>Gets the name of the registrar module used (e.g. "Namecheap", "Name.am").</summary>
    public string? Registrar { get; private set; }

    /// <summary>Gets the registration period in years.</summary>
    public int RegistrationPeriod { get; private set; } = 1;

    /// <summary>Gets the one-time registration cost.</summary>
    public decimal FirstPaymentAmount { get; private set; }

    /// <summary>Gets the payment method label (e.g. "Credit/Debit Card").</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Gets the applied promotion/coupon code.</summary>
    public string? PromotionCode { get; private set; }

    /// <summary>Gets the external payment subscription reference.</summary>
    public string? SubscriptionId { get; private set; }

    /// <summary>Gets the free-text admin notes.</summary>
    public string? AdminNotes { get; private set; }

    /// <summary>Gets the FK to the order that created this domain.</summary>
    public int? OrderId { get; private set; }

    /// <summary>Gets the order type: "Register" or "Transfer".</summary>
    public string OrderType { get; private set; } = "Register";

    /// <summary>Gets whether DNS management is enabled for this domain.</summary>
    public bool DnsManagement { get; private set; }

    /// <summary>Gets whether email forwarding is enabled for this domain.</summary>
    public bool EmailForwarding { get; private set; }

    /// <summary>Gets the read-only view of nameservers assigned to this domain.</summary>
    public IReadOnlyList<Nameserver> Nameservers => _nameservers.AsReadOnly();

    /// <summary>Gets the read-only view of DNS records in this domain's zone.</summary>
    public IReadOnlyList<DnsRecord> DnsRecords => _dnsRecords.AsReadOnly();

    /// <summary>Gets the read-only view of email forwarding rules for this domain.</summary>
    public IReadOnlyList<EmailForwardingRule> EmailForwardingRules => _emailForwardingRules.AsReadOnly();

    /// <summary>Gets the read-only view of expiry reminders sent for this domain.</summary>
    public IReadOnlyList<DomainReminder> Reminders => _reminders.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Domain() : base(0) { }

    /// <summary>
    /// Creates a new domain in <see cref="DomainStatus.PendingRegistration"/> status.
    /// Call <see cref="Activate"/> once the registrar confirms the registration.
    /// </summary>
    /// <param name="clientId">FK to the client requesting registration.</param>
    /// <param name="name">Fully-qualified domain name to register.</param>
    /// <param name="expiresAt">Expected expiry date returned by the registrar.</param>
    /// <param name="autoRenew">Whether to auto-renew at expiry.</param>
    /// <param name="whoisPrivacy">Whether to enable WHOIS privacy.</param>
    /// <param name="recurringAmount">Recurring registration price.</param>
    /// <param name="firstPaymentAmount">One-time registration cost.</param>
    /// <param name="priceCurrency">ISO 4217 currency code for the price.</param>
    /// <param name="registrar">Name of the registrar module used.</param>
    /// <param name="registrationPeriod">Registration period in years.</param>
    /// <returns>A new <see cref="Domain"/> in <see cref="DomainStatus.PendingRegistration"/> state.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static Domain Register(
        int clientId, string name, DateTimeOffset expiresAt, bool autoRenew, bool whoisPrivacy,
        decimal recurringAmount = 0, decimal firstPaymentAmount = 0, string priceCurrency = "USD",
        string? registrar = null, int registrationPeriod = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Domain
        {
            ClientId = clientId,
            Name = name,
            Tld = ExtractTld(name),
            Status = DomainStatus.PendingRegistration,
            RegisteredAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            NextDueDate = expiresAt,
            AutoRenew = autoRenew,
            WhoisPrivacy = whoisPrivacy,
            RecurringAmount = recurringAmount,
            FirstPaymentAmount = firstPaymentAmount,
            PriceCurrency = priceCurrency,
            Registrar = registrar,
            RegistrationPeriod = registrationPeriod,
            OrderType = "Register",
        };
    }

    /// <summary>
    /// Creates a new domain in <see cref="DomainStatus.PendingTransfer"/> status.
    /// Call <see cref="ActivateTransfer"/> once the registrar confirms the transfer.
    /// </summary>
    /// <param name="clientId">FK to the client requesting the transfer.</param>
    /// <param name="name">Fully-qualified domain name being transferred.</param>
    /// <returns>A new <see cref="Domain"/> in <see cref="DomainStatus.PendingTransfer"/> state.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static Domain CreateTransfer(int clientId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Domain
        {
            ClientId = clientId,
            Name = name,
            Tld = ExtractTld(name),
            Status = DomainStatus.PendingTransfer,
            RegisteredAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.MinValue,
            OrderType = "Transfer",
        };
    }

    /// <summary>
    /// Transitions a <see cref="DomainStatus.PendingRegistration"/> domain to <see cref="DomainStatus.Active"/>
    /// and raises <see cref="DomainRegisteredEvent"/>.
    /// </summary>
    /// <param name="registrarRef">Registrar-assigned reference for this domain.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.PendingRegistration"/> status.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="registrarRef"/> is null or whitespace.</exception>
    public void Activate(string registrarRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrarRef);
        if (Status != DomainStatus.PendingRegistration)
        {
            throw new InvalidOperationException(
                $"Cannot activate a domain in status {Status}. Expected {DomainStatus.PendingRegistration}.");
        }

        Status = DomainStatus.Active;
        RegistrarRef = registrarRef;
        AddDomainEvent(new DomainRegisteredEvent(Id, ClientId, Name));
    }

    /// <summary>
    /// Transitions a <see cref="DomainStatus.PendingTransfer"/> domain to <see cref="DomainStatus.Active"/>
    /// and raises <see cref="DomainTransferredInEvent"/>.
    /// </summary>
    /// <param name="registrarRef">Registrar-assigned reference for this domain.</param>
    /// <param name="expiresAt">Expiry date returned by the registrar after transfer.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.PendingTransfer"/> status.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="registrarRef"/> is null or whitespace.</exception>
    public void ActivateTransfer(string registrarRef, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrarRef);
        if (Status != DomainStatus.PendingTransfer)
        {
            throw new InvalidOperationException(
                $"Cannot activate transfer for a domain in status {Status}. Expected {DomainStatus.PendingTransfer}.");
        }

        Status = DomainStatus.Active;
        RegistrarRef = registrarRef;
        ExpiresAt = expiresAt;
        AddDomainEvent(new DomainTransferredInEvent(Id, ClientId, Name));
    }

    /// <summary>
    /// Transitions an <see cref="DomainStatus.Active"/> domain to <see cref="DomainStatus.Expired"/>
    /// and raises <see cref="DomainExpiredEvent"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.Active"/> status.
    /// </exception>
    public void MarkExpired()
    {
        if (Status != DomainStatus.Active)
        {
            throw new InvalidOperationException(
                $"Only Active domains can be marked expired; current status is {Status}.");
        }

        Status = DomainStatus.Expired;
        AddDomainEvent(new DomainExpiredEvent(Id, ClientId, LinkedServiceId));
    }

    /// <summary>
    /// Transitions an <see cref="DomainStatus.Expired"/> domain to <see cref="DomainStatus.Redemption"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.Expired"/> status.
    /// </exception>
    public void MarkRedemption()
    {
        if (Status != DomainStatus.Expired)
        {
            throw new InvalidOperationException(
                $"Only Expired domains can enter redemption; current status is {Status}.");
        }

        Status = DomainStatus.Redemption;
    }

    /// <summary>
    /// Transitions an <see cref="DomainStatus.Active"/> domain to <see cref="DomainStatus.Transferred"/>.
    /// Used when the domain has been transferred out to another registrar.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.Active"/> status.
    /// </exception>
    public void MarkTransferred()
    {
        if (Status != DomainStatus.Active)
        {
            throw new InvalidOperationException(
                $"Only Active domains can be marked transferred; current status is {Status}.");
        }

        Status = DomainStatus.Transferred;
    }

    /// <summary>
    /// Cancels this domain.
    /// Only valid for domains that are not Active, Transferred, or already Cancelled.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is Active, Transferred, or already Cancelled.
    /// </exception>
    public void Cancel()
    {
        if (Status is DomainStatus.Active or DomainStatus.Transferred or DomainStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Cannot cancel a domain in status {Status}.");
        }

        Status = DomainStatus.Cancelled;
    }

    /// <summary>
    /// Extends the domain's expiry date and raises <see cref="DomainRenewedEvent"/>.
    /// </summary>
    /// <param name="newExpiresAt">The new expiry date after renewal.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is Cancelled or Transferred.
    /// </exception>
    public void Renew(DateTimeOffset newExpiresAt)
    {
        if (Status is DomainStatus.Cancelled or DomainStatus.Transferred)
        {
            throw new InvalidOperationException(
                $"Cannot renew a domain in status {Status}.");
        }

        ExpiresAt = newExpiresAt;
        AddDomainEvent(new DomainRenewedEvent(Id, ClientId, newExpiresAt));
    }

    /// <summary>
    /// Enables or disables automatic renewal for this domain.
    /// </summary>
    /// <param name="value">The desired auto-renew flag value.</param>
    public void SetAutoRenew(bool value) => AutoRenew = value;

    /// <summary>
    /// Enables or disables WHOIS privacy for this domain.
    /// </summary>
    /// <param name="value">The desired WHOIS privacy flag value.</param>
    public void SetWhoisPrivacy(bool value) => WhoisPrivacy = value;

    /// <summary>
    /// Enables or disables the registrar transfer-lock for this domain.
    /// </summary>
    /// <param name="value">The desired lock flag value.</param>
    public void SetLock(bool value) => IsLocked = value;

    /// <summary>
    /// Sets the EPP (authorization) code used for outgoing transfers.
    /// </summary>
    /// <param name="code">The EPP code provided by the registrar.</param>
    public void SetEppCode(string code) => EppCode = code;

    /// <summary>
    /// Replaces the entire nameserver list for this domain.
    /// </summary>
    /// <param name="hosts">New ordered list of nameserver hostnames (minimum 2 required by most registrars).</param>
    public void SetNameservers(IReadOnlyList<string> hosts)
    {
        _nameservers.Clear();
        foreach (var host in hosts)
        {
            _nameservers.Add(new Nameserver(Id, host));
        }
    }

    /// <summary>
    /// Adds a new DNS record to this domain's zone.
    /// </summary>
    /// <param name="type">DNS record type.</param>
    /// <param name="host">Record host or name (e.g. "@", "www", "mail").</param>
    /// <param name="value">Record value (e.g. IP address, target hostname, TXT string).</param>
    /// <param name="ttl">Time-to-live in seconds.</param>
    /// <param name="priority">Priority for MX/SRV records; null for other types.</param>
    public void AddDnsRecord(DnsRecordType type, string host, string value, int ttl, int? priority)
    {
        _dnsRecords.Add(new DnsRecord(Id, type, host, value, ttl, priority));
    }

    /// <summary>
    /// Removes a DNS record from this domain's zone by its ID.
    /// </summary>
    /// <param name="recordId">The ID of the DNS record to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when no record with the given ID exists.</exception>
    public void RemoveDnsRecord(int recordId)
    {
        var record = _dnsRecords.FirstOrDefault(r => r.Id == recordId)
            ?? throw new InvalidOperationException($"DNS record with ID {recordId} not found on domain {Name}.");

        _dnsRecords.Remove(record);
    }

    /// <summary>
    /// Updates the value, TTL, and priority of an existing DNS record.
    /// </summary>
    /// <param name="recordId">The ID of the DNS record to update.</param>
    /// <param name="value">New record value.</param>
    /// <param name="ttl">New time-to-live in seconds.</param>
    /// <param name="priority">New priority, or null to clear it.</param>
    /// <exception cref="InvalidOperationException">Thrown when no record with the given ID exists.</exception>
    public void UpdateDnsRecord(int recordId, string value, int ttl, int? priority)
    {
        var record = _dnsRecords.FirstOrDefault(r => r.Id == recordId)
            ?? throw new InvalidOperationException($"DNS record with ID {recordId} not found on domain {Name}.");

        record.Update(value, ttl, priority);
    }

    /// <summary>
    /// Links a hosting service to this domain.
    /// Used to propagate domain expiry events to the associated service.
    /// </summary>
    /// <param name="serviceId">FK to the <c>ClientService</c> aggregate.</param>
    public void LinkService(int serviceId) => LinkedServiceId = serviceId;

    /// <summary>
    /// Updates the administrative and billing fields of this domain.
    /// </summary>
    /// <param name="firstPaymentAmount">One-time registration cost.</param>
    /// <param name="recurringAmount">Recurring registration price.</param>
    /// <param name="paymentMethod">Payment method label (e.g. "Credit/Debit Card").</param>
    /// <param name="promotionCode">Applied promotion/coupon code.</param>
    /// <param name="subscriptionId">External payment subscription reference.</param>
    /// <param name="adminNotes">Free-text admin notes.</param>
    /// <param name="expiresAt">UTC expiry date.</param>
    /// <param name="nextDueDate">Next renewal payment due date (UTC).</param>
    /// <param name="registrationPeriod">Registration period in years.</param>
    /// <param name="status">New lifecycle status.</param>
    public void Update(
        decimal firstPaymentAmount, decimal recurringAmount, string? paymentMethod,
        string? promotionCode, string? subscriptionId, string? adminNotes,
        DateTimeOffset expiresAt, DateTimeOffset nextDueDate, int registrationPeriod, DomainStatus status)
    {
        FirstPaymentAmount = firstPaymentAmount;
        RecurringAmount = recurringAmount;
        PaymentMethod = paymentMethod;
        PromotionCode = promotionCode;
        SubscriptionId = subscriptionId;
        AdminNotes = adminNotes;
        ExpiresAt = expiresAt;
        NextDueDate = nextDueDate;
        RegistrationPeriod = registrationPeriod;
        Status = status;
    }

    /// <summary>
    /// Enables or disables DNS management for this domain.
    /// </summary>
    /// <param name="value">The desired DNS management flag value.</param>
    public void SetDnsManagement(bool value) => DnsManagement = value;

    /// <summary>
    /// Enables or disables email forwarding for this domain.
    /// </summary>
    /// <param name="value">The desired email forwarding flag value.</param>
    public void SetEmailForwarding(bool value) => EmailForwarding = value;

    /// <summary>
    /// Adds a new email forwarding rule to this domain.
    /// </summary>
    /// <param name="source">Source alias or local part (e.g. "info").</param>
    /// <param name="destination">Destination email address.</param>
    public void AddForwardingRule(string source, string destination)
    {
        _emailForwardingRules.Add(new EmailForwardingRule(Id, source, destination));
    }

    /// <summary>
    /// Updates an existing email forwarding rule on this domain.
    /// </summary>
    /// <param name="ruleId">The ID of the forwarding rule to update.</param>
    /// <param name="source">New source alias.</param>
    /// <param name="destination">New destination email.</param>
    /// <param name="isActive">Whether the rule is active.</param>
    /// <exception cref="InvalidOperationException">Thrown when no rule with the given ID exists on this domain.</exception>
    public void UpdateForwardingRule(int ruleId, string source, string destination, bool isActive)
    {
        var rule = _emailForwardingRules.FirstOrDefault(r => r.Id == ruleId)
            ?? throw new InvalidOperationException($"Email forwarding rule with ID {ruleId} not found on domain {Name}.");
        rule.Update(source, destination, isActive);
    }

    /// <summary>
    /// Removes an email forwarding rule from this domain.
    /// </summary>
    /// <param name="ruleId">The ID of the forwarding rule to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when no rule with the given ID exists on this domain.</exception>
    public void RemoveForwardingRule(int ruleId)
    {
        var rule = _emailForwardingRules.FirstOrDefault(r => r.Id == ruleId)
            ?? throw new InvalidOperationException($"Email forwarding rule with ID {ruleId} not found on domain {Name}.");
        _emailForwardingRules.Remove(rule);
    }

    /// <summary>
    /// Records an expiry reminder that was sent for this domain.
    /// </summary>
    /// <param name="reminderType">Type label for the reminder (e.g. "30 Days Before Expiry").</param>
    /// <param name="sentTo">Recipient email address.</param>
    public void AddReminder(string reminderType, string sentTo)
    {
        _reminders.Add(new DomainReminder(Id, reminderType, sentTo));
    }

    /// <summary>
    /// Extracts the top-level domain suffix from a fully-qualified domain name.
    /// Returns the segment after the last dot (e.g. "com" from "example.com").
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <returns>The TLD suffix.</returns>
    private static string ExtractTld(string domainName)
    {
        var lastDot = domainName.LastIndexOf('.');
        return lastDot >= 0 ? domainName[(lastDot + 1)..] : domainName;
    }
}
