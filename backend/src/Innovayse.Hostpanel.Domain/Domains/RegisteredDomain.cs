using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Domains;

/// <summary>
/// Aggregate root representing a domain name registration owned by a client.
/// Owns collections of <see cref="Nameserver"/> and <see cref="DnsRecord"/> entities.
/// Stored in the <c>Domains</c> table.
/// </summary>
public sealed class RegisteredDomain : BaseEntity, IAggregateRoot
{
    /// <summary>Days before expiry the earliest expiry warning is due; the warning window of one expiry cycle.</summary>
    public const int ExpiryWarningWindowDays = 30;

    /// <summary>Expiry warnings from the nearest threshold to the earliest, so the first match is the nearest one due.</summary>
    private static readonly DomainReminderType[] ExpiryWarningsNearestFirst =
    [
        DomainReminderType.OneDayBeforeExpiry,
        DomainReminderType.SevenDaysBeforeExpiry,
        DomainReminderType.FourteenDaysBeforeExpiry,
        DomainReminderType.ThirtyDaysBeforeExpiry,
    ];

    /// <summary>Internal mutable list of nameservers assigned to this domain.</summary>
    private readonly List<Nameserver> _nameservers = [];

    /// <summary>Internal mutable list of DNS records in this domain's zone.</summary>
    private readonly List<DnsRecord> _dnsRecords = [];

    /// <summary>Internal mutable list of email forwarding rules for this domain.</summary>
    private readonly List<EmailForwardingRule> _emailForwardingRules = [];

    /// <summary>Internal mutable list of expiry reminders sent for this domain.</summary>
    private readonly List<DomainReminder> _reminders = [];

    /// <summary>Internal mutable set of the order lines whose renewal extended this domain.</summary>
    private readonly List<DomainLineRenewal> _lineRenewals = [];

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
    public string PriceCurrency { get; private set; } = string.Empty;

    /// <summary>Gets the next renewal payment due date (UTC).</summary>
    public DateTimeOffset NextDueDate { get; private set; }

    /// <summary>
    /// Gets the free-text name of the registrar as the source system recorded it; display data
    /// only, never used to route an operation.
    /// </summary>
    public string? Registrar { get; private set; }

    /// <summary>
    /// Gets the registrar module the domain was registered, transferred or imported with; every
    /// operation on the domain goes to it even after the TLD is configured for another registrar.
    /// Null for a row that predates the column; the TLD's configured registrar answers for it.
    /// </summary>
    public RegistrarModule? RegistrarModule { get; private set; }

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
    public string OrderType { get; private set; } = DomainOrderTypes.Register;

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

    /// <summary>
    /// Gets the read-only view of the order lines whose renewal extended this domain. A retry of a line finds its own row
    /// here and does not extend the domain a second time; renewals made any other way add and remove nothing.
    /// </summary>
    public IReadOnlyList<DomainLineRenewal> LineRenewals => _lineRenewals.AsReadOnly();

    /// <summary>
    /// Gets whether the domain's status allows a renewal: anything but
    /// <see cref="DomainStatus.Cancelled"/> or <see cref="DomainStatus.Transferred"/>. Read by the
    /// renewal use case before the registrar is called, so a refused renewal is never bought.
    /// </summary>
    public bool CanRenew => Status is not (DomainStatus.Cancelled or DomainStatus.Transferred);

    /// <summary>
    /// Gets whether a hosting service may be linked to this domain: a
    /// <see cref="DomainStatus.Cancelled"/> or <see cref="DomainStatus.Transferred"/> domain is no
    /// longer managed here, so its expiry would never reach the service.
    /// </summary>
    public bool CanLinkService => Status is not (DomainStatus.Cancelled or DomainStatus.Transferred);

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private RegisteredDomain() { }

    /// <summary>
    /// Creates a new domain in <see cref="DomainStatus.PendingRegistration"/> status.
    /// Call <see cref="Activate"/> once the registrar confirms the registration.
    /// </summary>
    /// <param name="clientId">FK to the client requesting registration.</param>
    /// <param name="name">Fully-qualified domain name to register.</param>
    /// <param name="registeredAt">The moment the registration was requested, UTC.</param>
    /// <param name="expiresAt">Expected expiry date returned by the registrar.</param>
    /// <param name="autoRenew">Whether to auto-renew at expiry.</param>
    /// <param name="whoisPrivacy">Whether to enable WHOIS privacy.</param>
    /// <param name="priceCurrency">ISO 4217 code of the currency the domain is billed in.</param>
    /// <param name="recurringAmount">Recurring registration price.</param>
    /// <param name="firstPaymentAmount">One-time registration cost.</param>
    /// <param name="registrar">Name of the registrar module used.</param>
    /// <param name="registrationPeriod">Registration period in years.</param>
    /// <param name="paymentMethod">Payment method label (e.g. "Credit/Debit Card"); null if not set.</param>
    /// <param name="registrarModule">The registrar module the registration was placed at.</param>
    /// <param name="orderId">The order whose line registers the domain, or <see langword="null"/> for a registration that is not an order's.</param>
    /// <returns>A new <see cref="Domain"/> in <see cref="DomainStatus.PendingRegistration"/> state.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="priceCurrency"/> is null or whitespace.
    /// </exception>
    public static RegisteredDomain Register(
        int clientId, string name, DateTimeOffset registeredAt, DateTimeOffset expiresAt, bool autoRenew, bool whoisPrivacy,
        string priceCurrency, decimal recurringAmount = 0, decimal firstPaymentAmount = 0,
        string? registrar = null, int registrationPeriod = 1, string? paymentMethod = null,
        RegistrarModule? registrarModule = null, int? orderId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(priceCurrency);
        return new RegisteredDomain
        {
            ClientId = clientId,
            Name = NormaliseName(name),
            Tld = ExtractTld(name),
            Status = DomainStatus.PendingRegistration,
            RegisteredAt = registeredAt,
            ExpiresAt = expiresAt,
            NextDueDate = expiresAt,
            AutoRenew = autoRenew,
            WhoisPrivacy = whoisPrivacy,
            RecurringAmount = recurringAmount,
            FirstPaymentAmount = firstPaymentAmount,
            PriceCurrency = priceCurrency,
            Registrar = registrar,
            RegistrarModule = registrarModule,
            RegistrationPeriod = registrationPeriod,
            PaymentMethod = paymentMethod,
            OrderId = orderId,
            OrderType = DomainOrderTypes.Register,
        };
    }

    /// <summary>
    /// Recreates a domain carried over from another system, already in the lifecycle state it had there.
    /// Unlike <see cref="Register"/> followed by <see cref="Activate"/> or <see cref="MarkExpired"/>, this
    /// raises no domain event: the registration and expiry happened in the source system long ago, so
    /// nothing may notify the client, invoice or suspend a linked service because of the import.
    /// <see cref="RegistrarRef"/> stays <see langword="null"/> because the source does not carry the
    /// registrar's reference; it is filled when the registrar is next synchronised.
    /// </summary>
    /// <param name="clientId">FK to the client that owns the domain.</param>
    /// <param name="name">Fully-qualified domain name.</param>
    /// <param name="status">The lifecycle status the domain had in the source system.</param>
    /// <param name="registeredAt">When the domain was registered, UTC.</param>
    /// <param name="expiresAt">When the registration expires at the registry, UTC.</param>
    /// <param name="autoRenew">Whether the domain renews automatically.</param>
    /// <param name="priceCurrency">ISO 4217 code of the currency the domain is billed in.</param>
    /// <param name="registrar">Name of the source registrar module; null when unknown.</param>
    /// <param name="registrarModule">The registrar module the domain is managed through here; null when unknown.</param>
    /// <returns>A new <see cref="RegisteredDomain"/> in <paramref name="status"/> with no pending domain events.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="priceCurrency"/> is null or whitespace.
    /// </exception>
    public static RegisteredDomain Import(
        int clientId, string name, DomainStatus status, DateTimeOffset registeredAt, DateTimeOffset expiresAt,
        bool autoRenew, string priceCurrency, string? registrar, RegistrarModule? registrarModule = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(priceCurrency);
        return new RegisteredDomain
        {
            ClientId = clientId,
            Name = NormaliseName(name),
            Tld = ExtractTld(name),
            Status = status,
            RegisteredAt = registeredAt,
            ExpiresAt = expiresAt,
            NextDueDate = expiresAt,
            AutoRenew = autoRenew,
            PriceCurrency = priceCurrency,
            Registrar = registrar,
            RegistrarModule = registrarModule,
            OrderType = DomainOrderTypes.Register,
        };
    }

    /// <summary>
    /// Creates a new domain in <see cref="DomainStatus.PendingTransfer"/> status.
    /// Call <see cref="ActivateTransfer"/> once the registrar confirms the transfer.
    /// </summary>
    /// <param name="clientId">FK to the client requesting the transfer.</param>
    /// <param name="name">Fully-qualified domain name being transferred.</param>
    /// <param name="registeredAt">The moment the transfer was requested, UTC.</param>
    /// <param name="priceCurrency">ISO 4217 code of the currency the domain is billed in.</param>
    /// <param name="firstPaymentAmount">One-time transfer cost.</param>
    /// <param name="recurringAmount">Recurring renewal price after transfer.</param>
    /// <param name="paymentMethod">Payment method label (e.g. "Credit/Debit Card"); null if not set.</param>
    /// <param name="registrationPeriod">Registration period bought with the transfer, in years.</param>
    /// <param name="registrarModule">The registrar module the transfer was placed at.</param>
    /// <param name="orderId">The order whose line transfers the domain in, or <see langword="null"/> for a transfer that is not an order's.</param>
    /// <returns>A new <see cref="Domain"/> in <see cref="DomainStatus.PendingTransfer"/> state.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="priceCurrency"/> is null or whitespace.
    /// </exception>
    public static RegisteredDomain CreateTransfer(
        int clientId, string name, DateTimeOffset registeredAt, string priceCurrency,
        decimal firstPaymentAmount = 0, decimal recurringAmount = 0, string? paymentMethod = null,
        int registrationPeriod = 1, RegistrarModule? registrarModule = null, int? orderId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(priceCurrency);
        return new RegisteredDomain
        {
            ClientId = clientId,
            Name = NormaliseName(name),
            Tld = ExtractTld(name),
            Status = DomainStatus.PendingTransfer,
            RegisteredAt = registeredAt,
            ExpiresAt = DateTimeOffset.MinValue,
            FirstPaymentAmount = firstPaymentAmount,
            RecurringAmount = recurringAmount,
            PriceCurrency = priceCurrency,
            PaymentMethod = paymentMethod,
            RegistrationPeriod = registrationPeriod,
            RegistrarModule = registrarModule,
            OrderId = orderId,
            OrderType = DomainOrderTypes.Transfer,
        };
    }

    /// <summary>
    /// Transitions a <see cref="DomainStatus.PendingRegistration"/> domain to <see cref="DomainStatus.Active"/>
    /// and raises <see cref="DomainRegisteredEvent"/>.
    /// </summary>
    /// <param name="registrarRef">Registrar-assigned reference for this domain.</param>
    /// <param name="expiresAt">
    /// Expiry date the registrar reports for the registration; the billing due date follows it.
    /// </param>
    /// <param name="occurredAt">The moment the registrar confirmed the registration, UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.PendingRegistration"/> status.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="registrarRef"/> is null or whitespace.</exception>
    public void Activate(string registrarRef, DateTimeOffset expiresAt, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrarRef);
        if (Status != DomainStatus.PendingRegistration)
        {
            throw new InvalidOperationException(
                $"Cannot activate a domain in status {Status}. Expected {DomainStatus.PendingRegistration}.");
        }

        Status = DomainStatus.Active;
        RegistrarRef = registrarRef;
        ExpiresAt = expiresAt;
        NextDueDate = expiresAt;
        AddDomainEvent(new DomainRegisteredEvent(Id, ClientId, Name) { OccurredAt = occurredAt });
    }

    /// <summary>
    /// Transitions a <see cref="DomainStatus.PendingTransfer"/> domain to <see cref="DomainStatus.Active"/>
    /// and raises <see cref="DomainTransferredInEvent"/>.
    /// </summary>
    /// <param name="registrarRef">Registrar-assigned reference for this domain.</param>
    /// <param name="expiresAt">Expiry date returned by the registrar after transfer.</param>
    /// <param name="occurredAt">The moment the registrar confirmed the transfer, UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.PendingTransfer"/> status.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="registrarRef"/> is null or whitespace.</exception>
    public void ActivateTransfer(string registrarRef, DateTimeOffset expiresAt, DateTimeOffset occurredAt)
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
        NextDueDate = expiresAt;
        AddDomainEvent(new DomainTransferredInEvent(Id, ClientId, Name) { OccurredAt = occurredAt });
    }

    /// <summary>
    /// Transitions an <see cref="DomainStatus.Active"/> domain to <see cref="DomainStatus.Expired"/>
    /// and raises <see cref="DomainExpiredEvent"/>.
    /// </summary>
    /// <param name="occurredAt">The moment the domain was found expired, UTC.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not in <see cref="DomainStatus.Active"/> status.
    /// </exception>
    public void MarkExpired(DateTimeOffset occurredAt)
    {
        if (Status != DomainStatus.Active)
        {
            throw new InvalidOperationException(
                $"Only Active domains can be marked expired; current status is {Status}.");
        }

        Status = DomainStatus.Expired;
        AddDomainEvent(new DomainExpiredEvent(Id, ClientId, LinkedServiceId) { OccurredAt = occurredAt });
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
    /// <exception cref="DomainRuleException">
    /// Thrown when the domain is Active, Transferred, or already Cancelled.
    /// </exception>
    public void Cancel()
    {
        if (Status is DomainStatus.Active or DomainStatus.Transferred or DomainStatus.Cancelled)
        {
            throw new DomainRuleException(
                DomainsErrors.DomainCancelNotAllowed, $"Cannot cancel a domain in status {Status}.");
        }

        Status = DomainStatus.Cancelled;
    }

    /// <summary>
    /// Extends the domain's expiry date, moves the billing due date with it, brings an
    /// <see cref="DomainStatus.Expired"/> or <see cref="DomainStatus.Redemption"/> domain back to
    /// <see cref="DomainStatus.Active"/> when the new expiry lies ahead, and raises
    /// <see cref="DomainRenewedEvent"/>.
    /// </summary>
    /// <param name="newExpiresAt">The new expiry date after renewal.</param>
    /// <param name="occurredAt">The moment the renewal was made, UTC.</param>
    /// <param name="orderItemId">The order line the renewal was bought on, or <see langword="null"/> for a renewal that is not an order line's.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown when the domain is Cancelled or Transferred.
    /// </exception>
    public void Renew(DateTimeOffset newExpiresAt, DateTimeOffset occurredAt, int? orderItemId = null)
    {
        if (!CanRenew)
        {
            throw new DomainRuleException(
                DomainsErrors.DomainRenewNotAllowed, $"Cannot renew a domain in status {Status}.");
        }

        if (orderItemId is { } lineId && !HasRenewedForLine(lineId))
        {
            _lineRenewals.Add(new DomainLineRenewal(Id, lineId, occurredAt));
        }

        ExpiresAt = newExpiresAt;
        NextDueDate = newExpiresAt;
        if ((Status is DomainStatus.Expired or DomainStatus.Redemption) && newExpiresAt > occurredAt)
        {
            Status = DomainStatus.Active;
        }

        AddDomainEvent(new DomainRenewedEvent(Id, ClientId, newExpiresAt) { OccurredAt = occurredAt });
    }

    /// <summary>Tells whether an order line already extended this domain.</summary>
    /// <param name="orderItemId">The order line.</param>
    /// <returns><see langword="true"/> when the line's renewal is on record; renewals made any other way never answer for a line.</returns>
    public bool HasRenewedForLine(int orderItemId)
    {
        return _lineRenewals.Any(r => r.OrderItemId == orderItemId);
    }

    /// <summary>
    /// Takes the expiry a registrar reported for a renewal that was not recorded, when it lies past the one on record: the
    /// registrar holds the truth, so a second renewal it made must not be hidden behind an earlier date. The billing due date
    /// moves with it. No <see cref="DomainRenewedEvent"/> is raised, because the renewal on record already raised one.
    /// </summary>
    /// <param name="reportedExpiresAt">The expiry date the registrar reported.</param>
    /// <returns><see langword="true"/> when the expiry moved; <see langword="false"/> when the one on record is as late or later.</returns>
    public bool AdoptLaterRegistrarExpiry(DateTimeOffset reportedExpiresAt)
    {
        if (reportedExpiresAt <= ExpiresAt)
        {
            return false;
        }

        ExpiresAt = reportedExpiresAt;
        NextDueDate = reportedExpiresAt;
        return true;
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
    /// <exception cref="DomainRuleException">Thrown when no record with the given ID exists.</exception>
    public void RemoveDnsRecord(int recordId)
    {
        var record = _dnsRecords.FirstOrDefault(r => r.Id == recordId)
            ?? throw new DomainRuleException(
                DomainsErrors.DomainDnsRecordNotFound, $"DNS record with ID {recordId} not found on domain {Name}.");

        _dnsRecords.Remove(record);
    }

    /// <summary>
    /// Updates the value, TTL, and priority of an existing DNS record.
    /// </summary>
    /// <param name="recordId">The ID of the DNS record to update.</param>
    /// <param name="value">New record value.</param>
    /// <param name="ttl">New time-to-live in seconds.</param>
    /// <param name="priority">New priority, or null to clear it.</param>
    /// <exception cref="DomainRuleException">Thrown when no record with the given ID exists.</exception>
    public void UpdateDnsRecord(int recordId, string value, int ttl, int? priority)
    {
        var record = _dnsRecords.FirstOrDefault(r => r.Id == recordId)
            ?? throw new DomainRuleException(
                DomainsErrors.DomainDnsRecordNotFound, $"DNS record with ID {recordId} not found on domain {Name}.");

        record.Update(value, ttl, priority);
    }

    /// <summary>
    /// Links a hosting service to this domain.
    /// Used to propagate domain expiry events to the associated service.
    /// </summary>
    /// <param name="serviceId">FK to the <c>ClientService</c> aggregate.</param>
    /// <exception cref="DomainRuleException">Thrown when the domain is Cancelled or Transferred.</exception>
    public void LinkService(int serviceId)
    {
        if (!CanLinkService)
        {
            throw new DomainRuleException(
                DomainsErrors.DomainLinkNotAllowed, $"Cannot link a service to a domain in status {Status}.");
        }

        LinkedServiceId = serviceId;
    }

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

    /// <summary>Moves the registration date to the moment the domain was really registered, for domains seeded from history.</summary>
    /// <param name="registeredAt">The moment the domain was registered (UTC).</param>
    public void Backdate(DateTimeOffset registeredAt) => RegisteredAt = registeredAt;

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
    /// <exception cref="DomainRuleException">Thrown when no rule with the given ID exists on this domain.</exception>
    public void UpdateForwardingRule(int ruleId, string source, string destination, bool isActive)
    {
        var rule = _emailForwardingRules.FirstOrDefault(r => r.Id == ruleId)
            ?? throw new DomainRuleException(
                DomainsErrors.DomainForwardingRuleNotFound,
                $"Email forwarding rule with ID {ruleId} not found on domain {Name}.");
        rule.Update(source, destination, isActive);
    }

    /// <summary>
    /// Removes an email forwarding rule from this domain.
    /// </summary>
    /// <param name="ruleId">The ID of the forwarding rule to remove.</param>
    /// <exception cref="DomainRuleException">Thrown when no rule with the given ID exists on this domain.</exception>
    public void RemoveForwardingRule(int ruleId)
    {
        var rule = _emailForwardingRules.FirstOrDefault(r => r.Id == ruleId)
            ?? throw new DomainRuleException(
                DomainsErrors.DomainForwardingRuleNotFound,
                $"Email forwarding rule with ID {ruleId} not found on domain {Name}.");
        _emailForwardingRules.Remove(rule);
    }

    /// <summary>
    /// Records an expiry reminder that was sent for this domain.
    /// </summary>
    /// <param name="reminderType">Kind of reminder that was sent.</param>
    /// <param name="sentTo">Recipient email address.</param>
    /// <param name="sentAt">When the reminder was sent, UTC.</param>
    public void AddReminder(DomainReminderType reminderType, string sentTo, DateTimeOffset sentAt)
    {
        _reminders.Add(new DomainReminder(Id, reminderType, sentTo, sentAt));
    }

    /// <summary>
    /// Warns of the coming expiry when a warning is due: records the reminder and raises
    /// <see cref="DomainExpiringEvent"/>, which goes out only once the save has committed.
    /// </summary>
    /// <remarks>
    /// Only the nearest threshold at or above the days left is sent, so a domain added late gets
    /// one warning and not a burst of every larger one on consecutive days. Reminders count only
    /// within the current expiry cycle (sent no earlier than <see cref="ExpiryWarningWindowDays"/>
    /// plus a day before <see cref="ExpiresAt"/>), so a renewal starts the warnings again.
    /// </remarks>
    /// <param name="sentTo">Recipient recorded on the reminder.</param>
    /// <param name="now">The current moment, UTC.</param>
    /// <returns>Whether a warning was recorded.</returns>
    public bool RemindOfExpiry(string sentTo, DateTimeOffset now)
    {
        if (Status != DomainStatus.Active || ExpiresAt <= now)
        {
            return false;
        }

        var daysLeft = (int)Math.Ceiling((ExpiresAt - now).TotalDays);
        if (daysLeft > ExpiryWarningWindowDays)
        {
            return false;
        }

        var due = ExpiryWarningsNearestFirst.First(w => DaysBeforeExpiry(w) >= daysLeft);
        var cycleStart = ExpiresAt.AddDays(-(ExpiryWarningWindowDays + 1));
        if (_reminders.Any(r => r.ReminderType == due && r.SentAt >= cycleStart))
        {
            return false;
        }

        AddReminder(due, sentTo, now);
        AddDomainEvent(new DomainExpiringEvent(Id, ClientId, Name, ExpiresAt) { OccurredAt = now });
        return true;
    }

    /// <summary>Returns how many days before expiry an expiry warning is due.</summary>
    /// <param name="reminderType">One of the <see cref="ExpiryWarningsNearestFirst"/>.</param>
    /// <returns>The warning threshold in days.</returns>
    private static int DaysBeforeExpiry(DomainReminderType reminderType)
    {
        return reminderType switch
        {
            DomainReminderType.OneDayBeforeExpiry => 1,
            DomainReminderType.SevenDaysBeforeExpiry => 7,
            DomainReminderType.FourteenDaysBeforeExpiry => 14,
            DomainReminderType.ThirtyDaysBeforeExpiry => ExpiryWarningWindowDays,
            _ => throw new ArgumentOutOfRangeException(nameof(reminderType), reminderType, "Not an expiry warning."),
        };
    }

    /// <summary>
    /// Extracts the top-level domain suffix from a fully-qualified domain name.
    /// Returns the segment after the last dot (e.g. "com" from "example.com").
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <returns>The TLD suffix.</returns>
    private static string ExtractTld(string domainName)
    {
        var normalised = NormaliseName(domainName);
        var lastDot = normalised.LastIndexOf('.');
        return lastDot >= 0 ? normalised[(lastDot + 1)..] : normalised;
    }

    /// <summary>
    /// Brings a domain name to its one stored form: trimmed and lower case. Domain names are case
    /// insensitive, so the unique index on <see cref="Name"/> only means "one row per domain" when
    /// <c>Example.com</c> and <c>example.com</c> are the same string.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name as given.</param>
    /// <returns>The trimmed, lower-case invariant name.</returns>
    public static string NormaliseName(string domainName)
    {
        return domainName.Trim().ToLowerInvariant();
    }
}
