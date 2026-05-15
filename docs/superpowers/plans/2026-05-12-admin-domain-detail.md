# Admin Domain Detail Page — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a full domain detail sub-page in the admin client profile, matching the WHMCS domain management page with editable fields, registrar commands, management toggles, email forwarding, DNS records, and domain reminders.

**Architecture:** Extend the existing Domain aggregate with new properties and owned collections (EmailForwardingRule, DomainReminder). Add CQRS commands/handlers following the existing pattern. Build Vue admin pages reusing existing components (ToggleSwitch, AppSelect) and utilities (formatDate, toDateInputValue, useApi). Extract sub-components for DNS, email forwarding, and reminders tables.

**Tech Stack:** C# 12 / ASP.NET Core 8 / EF Core 8 / Wolverine / FluentValidation / Vue 3 / TypeScript / Pinia / Tailwind CSS

**Key Constraint:** Reuse existing UI components and utilities. No redundant code.

---

## Task 1: Backend — New Domain Entities

**Files:**
- Create: `backend/src/Innovayse.Domain/Domains/EmailForwardingRule.cs`
- Create: `backend/src/Innovayse.Domain/Domains/DomainReminder.cs`
- Create: `backend/src/Innovayse.Domain/Domains/DomainContact.cs`

- [ ] **Step 1: Create EmailForwardingRule entity**

```csharp
// backend/src/Innovayse.Domain/Domains/EmailForwardingRule.cs
namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;

/// <summary>
/// Represents an email forwarding rule that routes mail from a source alias
/// on the domain to an external destination address.
/// </summary>
public sealed class EmailForwardingRule : Entity
{
    /// <summary>Gets the FK to the owning domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>Gets the source alias or local part (e.g. "info", "support").</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>Gets the destination email address.</summary>
    public string Destination { get; private set; } = string.Empty;

    /// <summary>Gets whether this forwarding rule is currently active.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private EmailForwardingRule() : base(0) { }

    /// <summary>
    /// Creates a new email forwarding rule.
    /// </summary>
    /// <param name="domainId">FK to the owning domain.</param>
    /// <param name="source">Source alias or local part.</param>
    /// <param name="destination">Destination email address.</param>
    internal EmailForwardingRule(int domainId, string source, string destination) : base(0)
    {
        DomainId = domainId;
        Source = source;
        Destination = destination;
    }

    /// <summary>
    /// Updates this forwarding rule's properties.
    /// </summary>
    /// <param name="source">New source alias.</param>
    /// <param name="destination">New destination email.</param>
    /// <param name="isActive">Whether the rule is active.</param>
    internal void Update(string source, string destination, bool isActive)
    {
        Source = source;
        Destination = destination;
        IsActive = isActive;
    }
}
```

- [ ] **Step 2: Create DomainReminder entity**

```csharp
// backend/src/Innovayse.Domain/Domains/DomainReminder.cs
namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;

/// <summary>
/// Tracks a reminder email sent to a client about domain expiry.
/// Created by the notification system; read-only from admin perspective.
/// </summary>
public sealed class DomainReminder : Entity
{
    /// <summary>Gets the FK to the owning domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>Gets the type of reminder (e.g. "30 Days Before Expiry").</summary>
    public string ReminderType { get; private set; } = string.Empty;

    /// <summary>Gets the recipient email address.</summary>
    public string SentTo { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the reminder was sent.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private DomainReminder() : base(0) { }

    /// <summary>
    /// Creates a new domain reminder record.
    /// </summary>
    /// <param name="domainId">FK to the owning domain.</param>
    /// <param name="reminderType">Type label for this reminder.</param>
    /// <param name="sentTo">Recipient email address.</param>
    internal DomainReminder(int domainId, string reminderType, string sentTo) : base(0)
    {
        DomainId = domainId;
        ReminderType = reminderType;
        SentTo = sentTo;
        SentAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 3: Create DomainContact value object**

```csharp
// backend/src/Innovayse.Domain/Domains/DomainContact.cs
namespace Innovayse.Domain.Domains;

/// <summary>
/// Value object representing WHOIS registrant contact details.
/// Not persisted — used as a parameter for registrar API calls.
/// </summary>
/// <param name="FirstName">Registrant first name.</param>
/// <param name="LastName">Registrant last name.</param>
/// <param name="Organization">Organization or company name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact phone number.</param>
/// <param name="Address1">Primary street address.</param>
/// <param name="Address2">Secondary address line.</param>
/// <param name="City">City name.</param>
/// <param name="State">State or province.</param>
/// <param name="PostalCode">Postal or ZIP code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
public record DomainContact(
    string FirstName,
    string LastName,
    string? Organization,
    string Email,
    string Phone,
    string Address1,
    string? Address2,
    string City,
    string State,
    string PostalCode,
    string Country);
```

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.Domain/Domains/EmailForwardingRule.cs \
       backend/src/Innovayse.Domain/Domains/DomainReminder.cs \
       backend/src/Innovayse.Domain/Domains/DomainContact.cs
git commit -m "feat(domain): add EmailForwardingRule, DomainReminder entities and DomainContact record"
```

---

## Task 2: Backend — Extend Domain Aggregate

**Files:**
- Modify: `backend/src/Innovayse.Domain/Domains/Domain.cs`

- [ ] **Step 1: Add new properties and collections to Domain.cs**

Add these new properties after the existing `RegistrationPeriod` property:

```csharp
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
```

Add new collection backing fields alongside the existing `_nameservers` and `_dnsRecords`:

```csharp
/// <summary>Internal mutable list of email forwarding rules for this domain.</summary>
private readonly List<EmailForwardingRule> _emailForwardingRules = [];

/// <summary>Internal mutable list of expiry reminders sent for this domain.</summary>
private readonly List<DomainReminder> _reminders = [];
```

Add public read-only accessors alongside the existing `Nameservers` and `DnsRecords`:

```csharp
/// <summary>Gets the read-only view of email forwarding rules for this domain.</summary>
public IReadOnlyList<EmailForwardingRule> EmailForwardingRules => _emailForwardingRules.AsReadOnly();

/// <summary>Gets the read-only view of expiry reminders sent for this domain.</summary>
public IReadOnlyList<DomainReminder> Reminders => _reminders.AsReadOnly();
```

- [ ] **Step 2: Rename Price to RecurringAmount**

In Domain.cs, rename the existing `Price` property:

```csharp
// BEFORE:
/// <summary>Gets the recurring registration price.</summary>
public decimal Price { get; private set; }

// AFTER:
/// <summary>Gets the recurring renewal amount.</summary>
public decimal RecurringAmount { get; private set; }
```

Update all references to `Price` within Domain.cs (the `Register` factory method):

```csharp
// In Register() factory method, change:
Price = price,
// To:
RecurringAmount = price,
```

Also update the `Register` factory parameter name from `price` to `recurringAmount` and add `firstPaymentAmount`:

```csharp
public static Domain Register(
    int clientId, string name, DateTimeOffset expiresAt, bool autoRenew, bool whoisPrivacy,
    decimal recurringAmount = 0, decimal firstPaymentAmount = 0,
    string priceCurrency = "USD", string? registrar = null, int registrationPeriod = 1)
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
```

Update `CreateTransfer` to set `OrderType`:

```csharp
// Add to the object initializer in CreateTransfer():
OrderType = "Transfer",
```

- [ ] **Step 3: Add new domain methods**

Add after the existing `LinkService` method:

```csharp
/// <summary>
/// Updates the editable domain fields from admin input.
/// </summary>
/// <param name="firstPaymentAmount">One-time registration cost.</param>
/// <param name="recurringAmount">Recurring renewal amount.</param>
/// <param name="paymentMethod">Payment method label.</param>
/// <param name="promotionCode">Applied promotion code.</param>
/// <param name="subscriptionId">External subscription reference.</param>
/// <param name="adminNotes">Free-text admin notes.</param>
/// <param name="expiresAt">New expiry date.</param>
/// <param name="nextDueDate">New next due date.</param>
/// <param name="registrationPeriod">Registration period in years.</param>
/// <param name="status">New domain status.</param>
public void Update(
    decimal firstPaymentAmount,
    decimal recurringAmount,
    string? paymentMethod,
    string? promotionCode,
    string? subscriptionId,
    string? adminNotes,
    DateTimeOffset expiresAt,
    DateTimeOffset nextDueDate,
    int registrationPeriod,
    DomainStatus status)
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

/// <summary>Enables or disables DNS management for this domain.</summary>
/// <param name="value">The desired DNS management flag value.</param>
public void SetDnsManagement(bool value) => DnsManagement = value;

/// <summary>Enables or disables email forwarding for this domain.</summary>
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
/// Updates an existing email forwarding rule.
/// </summary>
/// <param name="ruleId">The ID of the rule to update.</param>
/// <param name="source">New source alias.</param>
/// <param name="destination">New destination email.</param>
/// <param name="isActive">Whether the rule is active.</param>
/// <exception cref="InvalidOperationException">Thrown when no rule with the given ID exists.</exception>
public void UpdateForwardingRule(int ruleId, string source, string destination, bool isActive)
{
    var rule = _emailForwardingRules.FirstOrDefault(r => r.Id == ruleId)
        ?? throw new InvalidOperationException($"Email forwarding rule with ID {ruleId} not found on domain {Name}.");
    rule.Update(source, destination, isActive);
}

/// <summary>
/// Removes an email forwarding rule by ID.
/// </summary>
/// <param name="ruleId">The ID of the rule to remove.</param>
/// <exception cref="InvalidOperationException">Thrown when no rule with the given ID exists.</exception>
public void RemoveForwardingRule(int ruleId)
{
    var rule = _emailForwardingRules.FirstOrDefault(r => r.Id == ruleId)
        ?? throw new InvalidOperationException($"Email forwarding rule with ID {ruleId} not found on domain {Name}.");
    _emailForwardingRules.Remove(rule);
}

/// <summary>
/// Logs a reminder that was sent for this domain's expiry.
/// </summary>
/// <param name="reminderType">Type of reminder (e.g. "30 Days Before Expiry").</param>
/// <param name="sentTo">Recipient email address.</param>
public void AddReminder(string reminderType, string sentTo)
{
    _reminders.Add(new DomainReminder(Id, reminderType, sentTo));
}
```

- [ ] **Step 4: Update RegisterDomainCommand callers**

Search for all callers of `Domain.Register()` in the Application layer and update the parameter order. The `RegisterDomainHandler` will need updating since `price` was renamed to `recurringAmount` and `firstPaymentAmount` was added.

Look at `backend/src/Innovayse.Application/Domains/Commands/RegisterDomain/RegisterDomainHandler.cs` — update the `Register()` call to pass the renamed parameters.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Domain/Domains/Domain.cs
git commit -m "feat(domain): add new properties, rename Price→RecurringAmount, add Update/forwarding/reminder methods"
```

---

## Task 3: Backend — IRegistrarProvider & Stub Implementation

**Files:**
- Modify: `backend/src/Innovayse.Domain/Domains/Interfaces/IRegistrarProvider.cs`
- Modify: `backend/src/Innovayse.Infrastructure/Domains/Namecheap/NamecheapRegistrarProvider.cs`

- [ ] **Step 1: Add new methods to IRegistrarProvider**

Add after the existing `GetWhoisAsync` method:

```csharp
/// <summary>Modifies the WHOIS registrant contact details at the registrar.</summary>
/// <param name="domainName">The fully-qualified domain name.</param>
/// <param name="contact">Updated registrant contact details.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Result indicating success or failure.</returns>
Task<RegistrarResult> ModifyContactDetailsAsync(string domainName, DomainContact contact, CancellationToken ct);

/// <summary>Enables or disables email forwarding at the registrar.</summary>
/// <param name="domainName">The fully-qualified domain name.</param>
/// <param name="enabled">Whether to enable or disable.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Result indicating success or failure.</returns>
Task<RegistrarResult> SetEmailForwardingAsync(string domainName, bool enabled, CancellationToken ct);

/// <summary>Adds an email forwarding rule at the registrar.</summary>
/// <param name="domainName">The fully-qualified domain name.</param>
/// <param name="source">Source alias.</param>
/// <param name="destination">Destination email address.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Result indicating success or failure.</returns>
Task<RegistrarResult> AddEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct);

/// <summary>Updates an email forwarding rule at the registrar.</summary>
/// <param name="domainName">The fully-qualified domain name.</param>
/// <param name="source">Source alias to update.</param>
/// <param name="destination">New destination email address.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Result indicating success or failure.</returns>
Task<RegistrarResult> UpdateEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct);

/// <summary>Deletes an email forwarding rule at the registrar.</summary>
/// <param name="domainName">The fully-qualified domain name.</param>
/// <param name="source">Source alias to delete.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Result indicating success or failure.</returns>
Task<RegistrarResult> DeleteEmailForwardingRuleAsync(string domainName, string source, CancellationToken ct);

/// <summary>Enables or disables DNS management at the registrar.</summary>
/// <param name="domainName">The fully-qualified domain name.</param>
/// <param name="enabled">Whether to enable or disable.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Result indicating success or failure.</returns>
Task<RegistrarResult> SetDnsManagementAsync(string domainName, bool enabled, CancellationToken ct);
```

- [ ] **Step 2: Add stub implementations to NamecheapRegistrarProvider**

Add stub methods that return success:

```csharp
/// <inheritdoc />
public Task<RegistrarResult> ModifyContactDetailsAsync(string domainName, DomainContact contact, CancellationToken ct)
    => Task.FromResult(new RegistrarResult(true, null, null, null));

/// <inheritdoc />
public Task<RegistrarResult> SetEmailForwardingAsync(string domainName, bool enabled, CancellationToken ct)
    => Task.FromResult(new RegistrarResult(true, null, null, null));

/// <inheritdoc />
public Task<RegistrarResult> AddEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct)
    => Task.FromResult(new RegistrarResult(true, null, null, null));

/// <inheritdoc />
public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct)
    => Task.FromResult(new RegistrarResult(true, null, null, null));

/// <inheritdoc />
public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(string domainName, string source, CancellationToken ct)
    => Task.FromResult(new RegistrarResult(true, null, null, null));

/// <inheritdoc />
public Task<RegistrarResult> SetDnsManagementAsync(string domainName, bool enabled, CancellationToken ct)
    => Task.FromResult(new RegistrarResult(true, null, null, null));
```

- [ ] **Step 3: Commit**

```bash
git add backend/src/Innovayse.Domain/Domains/Interfaces/IRegistrarProvider.cs \
       backend/src/Innovayse.Infrastructure/Domains/Namecheap/NamecheapRegistrarProvider.cs
git commit -m "feat(domain): add registrar provider methods for contact, email forwarding, DNS management"
```

---

## Task 4: Backend — EF Configurations & Migration

**Files:**
- Modify: `backend/src/Innovayse.Infrastructure/Domains/Configurations/DomainConfiguration.cs`
- Create: `backend/src/Innovayse.Infrastructure/Domains/Configurations/EmailForwardingRuleConfiguration.cs`
- Create: `backend/src/Innovayse.Infrastructure/Domains/Configurations/DomainReminderConfiguration.cs`
- Modify: `backend/src/Innovayse.Infrastructure/Domains/DomainRepository.cs`

- [ ] **Step 1: Update DomainConfiguration.cs**

Add new column mappings. The existing `Price` → rename to `RecurringAmount`. Add all new properties:

```csharp
// Rename Price to RecurringAmount
builder.Property(d => d.RecurringAmount)
    .HasColumnType("decimal(18,2)")
    .HasDefaultValue(0m);

// New properties
builder.Property(d => d.FirstPaymentAmount)
    .HasColumnType("decimal(18,2)")
    .HasDefaultValue(0m);

builder.Property(d => d.PaymentMethod).HasMaxLength(100);
builder.Property(d => d.PromotionCode).HasMaxLength(100);
builder.Property(d => d.SubscriptionId).HasMaxLength(255);
builder.Property(d => d.AdminNotes).HasMaxLength(4000);
builder.Property(d => d.OrderType).HasMaxLength(20).HasDefaultValue("Register");

// New owned collections
builder.HasMany(d => d.EmailForwardingRules)
    .WithOne()
    .HasForeignKey(r => r.DomainId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Navigation(d => d.EmailForwardingRules)
    .UsePropertyAccessMode(PropertyAccessMode.Field)
    .HasField("_emailForwardingRules");

builder.HasMany(d => d.Reminders)
    .WithOne()
    .HasForeignKey(r => r.DomainId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Navigation(d => d.Reminders)
    .UsePropertyAccessMode(PropertyAccessMode.Field)
    .HasField("_reminders");
```

Remove the old `Price` mapping and replace with `RecurringAmount`.

- [ ] **Step 2: Create EmailForwardingRuleConfiguration.cs**

```csharp
// backend/src/Innovayse.Infrastructure/Domains/Configurations/EmailForwardingRuleConfiguration.cs
namespace Innovayse.Infrastructure.Domains.Configurations;

using Innovayse.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="EmailForwardingRule"/> entity.
/// Maps to the <c>email_forwarding_rules</c> table.
/// </summary>
internal sealed class EmailForwardingRuleConfiguration : IEntityTypeConfiguration<EmailForwardingRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailForwardingRule> builder)
    {
        builder.ToTable("email_forwarding_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.DomainId).IsRequired();
        builder.Property(r => r.Source).IsRequired().HasMaxLength(255);
        builder.Property(r => r.Destination).IsRequired().HasMaxLength(320);
        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasIndex(r => r.DomainId);
    }
}
```

- [ ] **Step 3: Create DomainReminderConfiguration.cs**

```csharp
// backend/src/Innovayse.Infrastructure/Domains/Configurations/DomainReminderConfiguration.cs
namespace Innovayse.Infrastructure.Domains.Configurations;

using Innovayse.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="DomainReminder"/> entity.
/// Maps to the <c>domain_reminders</c> table.
/// </summary>
internal sealed class DomainReminderConfiguration : IEntityTypeConfiguration<DomainReminder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DomainReminder> builder)
    {
        builder.ToTable("domain_reminders");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.DomainId).IsRequired();
        builder.Property(r => r.ReminderType).IsRequired().HasMaxLength(100);
        builder.Property(r => r.SentTo).IsRequired().HasMaxLength(320);

        builder.HasIndex(r => r.DomainId);
    }
}
```

- [ ] **Step 4: Update DomainRepository.cs — eager load new collections**

In `FindByIdAsync`, add `.Include(d => d.EmailForwardingRules)` and `.Include(d => d.Reminders)` alongside the existing Nameservers and DnsRecords includes.

- [ ] **Step 5: Generate EF migration**

```bash
cd backend/src/Innovayse.API
dotnet ef migrations add AddDomainDetailFields --project ../Innovayse.Infrastructure/Innovayse.Infrastructure.csproj
```

Review the generated migration to confirm:
- `Price` column renamed to `RecurringAmount`
- New columns added with correct defaults
- `email_forwarding_rules` and `domain_reminders` tables created

- [ ] **Step 6: Commit**

```bash
git add backend/src/Innovayse.Infrastructure/Domains/
git commit -m "feat(infra): add EF configs for EmailForwardingRule, DomainReminder; update Domain mapping"
```

---

## Task 5: Backend — New DTOs

**Files:**
- Modify: `backend/src/Innovayse.Application/Domains/DTOs/DomainDto.cs`
- Create: `backend/src/Innovayse.Application/Domains/DTOs/EmailForwardingRuleDto.cs`
- Create: `backend/src/Innovayse.Application/Domains/DTOs/DomainReminderDto.cs`

- [ ] **Step 1: Create EmailForwardingRuleDto**

```csharp
// backend/src/Innovayse.Application/Domains/DTOs/EmailForwardingRuleDto.cs
namespace Innovayse.Application.Domains.DTOs;

/// <summary>DTO for an email forwarding rule.</summary>
/// <param name="Id">Rule primary key.</param>
/// <param name="Source">Source alias or local part.</param>
/// <param name="Destination">Destination email address.</param>
/// <param name="IsActive">Whether the rule is currently active.</param>
public record EmailForwardingRuleDto(int Id, string Source, string Destination, bool IsActive);
```

- [ ] **Step 2: Create DomainReminderDto**

```csharp
// backend/src/Innovayse.Application/Domains/DTOs/DomainReminderDto.cs
namespace Innovayse.Application.Domains.DTOs;

/// <summary>DTO for a domain expiry reminder record.</summary>
/// <param name="Id">Reminder primary key.</param>
/// <param name="ReminderType">Type of reminder sent.</param>
/// <param name="SentTo">Recipient email address.</param>
/// <param name="SentAt">UTC timestamp when the reminder was sent.</param>
public record DomainReminderDto(int Id, string ReminderType, string SentTo, DateTimeOffset SentAt);
```

- [ ] **Step 3: Update DomainDto with new fields**

Add to the existing DomainDto record:

```csharp
// Add these parameters to the DomainDto record:
decimal FirstPaymentAmount,
decimal RecurringAmount,     // was Price
string? PaymentMethod,
string? PromotionCode,
string? SubscriptionId,
string? AdminNotes,
int? OrderId,
string OrderType,
bool DnsManagement,
bool EmailForwarding,
IReadOnlyList<EmailForwardingRuleDto> EmailForwardingRules,
IReadOnlyList<DomainReminderDto> Reminders
```

Remove the old `Price` parameter if it exists, replace with `RecurringAmount`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.Application/Domains/DTOs/
git commit -m "feat(app): add EmailForwardingRuleDto, DomainReminderDto; extend DomainDto"
```

---

## Task 6: Backend — Update Existing Queries

**Files:**
- Modify: `backend/src/Innovayse.Application/Domains/Queries/GetDomain/GetDomainQuery.cs`
- Modify: `backend/src/Innovayse.Application/Domains/Queries/GetDomain/GetDomainHandler.cs`
- Modify: `backend/src/Innovayse.Application/Domains/Queries/ListDomains/ListDomainsQuery.cs`
- Modify: `backend/src/Innovayse.Application/Domains/Queries/ListDomains/ListDomainsHandler.cs`
- Modify: `backend/src/Innovayse.Application/Domains/DTOs/DomainRegistrationDto.cs`

- [ ] **Step 1: Update GetDomainHandler to map new fields**

In the handler's mapping logic, add the new fields to the DomainDto construction. Map `EmailForwardingRules` and `Reminders` collections using the new DTOs:

```csharp
EmailForwardingRules = domain.EmailForwardingRules
    .Select(r => new EmailForwardingRuleDto(r.Id, r.Source, r.Destination, r.IsActive))
    .ToList(),
Reminders = domain.Reminders
    .Select(r => new DomainReminderDto(r.Id, r.ReminderType, r.SentTo, r.SentAt))
    .ToList(),
```

Update `Price` references to `RecurringAmount` in the mapping.

- [ ] **Step 2: Add ClientId filter to ListDomainsQuery**

```csharp
// Update the record:
public record ListDomainsQuery(int Page, int PageSize, int? ClientId = null);
```

- [ ] **Step 3: Update ListDomainsHandler to filter by ClientId**

In the handler, apply the optional client filter before pagination:

```csharp
var query = _context.Set<Domain>().AsQueryable();

if (request.ClientId.HasValue)
{
    query = query.Where(d => d.ClientId == request.ClientId.Value);
}

// Continue with existing pagination logic using `query` instead of raw DbSet
```

- [ ] **Step 4: Update DomainRegistrationDto — rename Price to RecurringAmount**

In the existing `DomainRegistrationDto`, rename the `Price` parameter to `RecurringAmount`. Update the mapping in `ListDomainsHandler` accordingly.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Application/Domains/
git commit -m "feat(app): update GetDomain to include new fields; add ClientId filter to ListDomains"
```

---

## Task 7: Backend — New Commands (UpdateDomain, ModifyContact, Toggles)

**Files:**
- Create: `backend/src/Innovayse.Application/Domains/Commands/UpdateDomain/UpdateDomainCommand.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/UpdateDomain/UpdateDomainHandler.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/ModifyDomainContact/ModifyDomainContactCommand.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/ModifyDomainContact/ModifyDomainContactHandler.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/SetDnsManagement/SetDnsManagementCommand.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/SetDnsManagement/SetDnsManagementHandler.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/SetEmailForwarding/SetEmailForwardingCommand.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/SetEmailForwarding/SetEmailForwardingHandler.cs`

- [ ] **Step 1: Create UpdateDomainCommand + Handler**

```csharp
// UpdateDomainCommand.cs
namespace Innovayse.Application.Domains.Commands.UpdateDomain;

/// <summary>Command to update all editable domain fields.</summary>
/// <param name="DomainId">Domain primary key.</param>
/// <param name="FirstPaymentAmount">One-time registration cost.</param>
/// <param name="RecurringAmount">Recurring renewal amount.</param>
/// <param name="PaymentMethod">Payment method label.</param>
/// <param name="PromotionCode">Applied promotion code.</param>
/// <param name="SubscriptionId">External subscription reference.</param>
/// <param name="AdminNotes">Free-text admin notes.</param>
/// <param name="ExpiresAt">Expiry date (ISO 8601).</param>
/// <param name="NextDueDate">Next due date (ISO 8601).</param>
/// <param name="RegistrationPeriod">Registration period in years.</param>
/// <param name="Status">Domain lifecycle status.</param>
/// <param name="Nameservers">Ordered list of nameserver hostnames.</param>
public record UpdateDomainCommand(
    int DomainId,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    string? PaymentMethod,
    string? PromotionCode,
    string? SubscriptionId,
    string? AdminNotes,
    string ExpiresAt,
    string NextDueDate,
    int RegistrationPeriod,
    string Status,
    List<string> Nameservers);
```

```csharp
// UpdateDomainHandler.cs
namespace Innovayse.Application.Domains.Commands.UpdateDomain;

using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Common;

/// <summary>Handles <see cref="UpdateDomainCommand"/> by updating domain fields and nameservers.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class UpdateDomainHandler(IDomainRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates the domain with the given fields and persists changes.
    /// </summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(UpdateDomainCommand command, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(command.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain with ID {command.DomainId} not found.");

        var status = Enum.Parse<DomainStatus>(command.Status);
        var expiresAt = DateTimeOffset.Parse(command.ExpiresAt);
        var nextDueDate = DateTimeOffset.Parse(command.NextDueDate);

        domain.Update(
            command.FirstPaymentAmount,
            command.RecurringAmount,
            command.PaymentMethod,
            command.PromotionCode,
            command.SubscriptionId,
            command.AdminNotes,
            expiresAt,
            nextDueDate,
            command.RegistrationPeriod,
            status);

        var nameserverHosts = command.Nameservers
            .Where(ns => !string.IsNullOrWhiteSpace(ns))
            .ToList();

        if (nameserverHosts.Count > 0)
        {
            domain.SetNameservers(nameserverHosts);
        }

        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Create ModifyDomainContactCommand + Handler**

```csharp
// ModifyDomainContactCommand.cs
namespace Innovayse.Application.Domains.Commands.ModifyDomainContact;

using Innovayse.Domain.Domains;

/// <summary>Command to modify the WHOIS registrant contact at the registrar.</summary>
/// <param name="DomainId">Domain primary key.</param>
/// <param name="Contact">Updated contact details.</param>
public record ModifyDomainContactCommand(int DomainId, DomainContact Contact);
```

```csharp
// ModifyDomainContactHandler.cs
namespace Innovayse.Application.Domains.Commands.ModifyDomainContact;

using Innovayse.Domain.Domains.Interfaces;

/// <summary>Handles <see cref="ModifyDomainContactCommand"/> by calling the registrar API.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="registrar">Registrar provider.</param>
public sealed class ModifyDomainContactHandler(IDomainRepository repo, IRegistrarProvider registrar)
{
    /// <summary>
    /// Sends updated contact details to the registrar.
    /// </summary>
    /// <param name="command">The modify contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ModifyDomainContactCommand command, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(command.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain with ID {command.DomainId} not found.");

        var result = await registrar.ModifyContactDetailsAsync(domain.Name, command.Contact, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to modify contact details at registrar.");
        }
    }
}
```

- [ ] **Step 3: Create SetDnsManagementCommand + Handler**

Follow the exact pattern of the existing `SetAutoRenewCommand`/`SetAutoRenewHandler`:

```csharp
// SetDnsManagementCommand.cs
namespace Innovayse.Application.Domains.Commands.SetDnsManagement;

/// <summary>Command to toggle DNS management on a domain.</summary>
/// <param name="DomainId">Domain primary key.</param>
/// <param name="Enabled">Whether DNS management should be enabled.</param>
public record SetDnsManagementCommand(int DomainId, bool Enabled);
```

```csharp
// SetDnsManagementHandler.cs
namespace Innovayse.Application.Domains.Commands.SetDnsManagement;

using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Common;

/// <summary>Handles <see cref="SetDnsManagementCommand"/>.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="registrar">Registrar provider.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class SetDnsManagementHandler(IDomainRepository repo, IRegistrarProvider registrar, IUnitOfWork uow)
{
    /// <summary>
    /// Toggles DNS management at the registrar and on the domain entity.
    /// </summary>
    /// <param name="command">The toggle command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(SetDnsManagementCommand command, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(command.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain with ID {command.DomainId} not found.");

        var result = await registrar.SetDnsManagementAsync(domain.Name, command.Enabled, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to update DNS management at registrar.");
        }

        domain.SetDnsManagement(command.Enabled);
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Create SetEmailForwardingCommand + Handler**

Same pattern as SetDnsManagement:

```csharp
// SetEmailForwardingCommand.cs
namespace Innovayse.Application.Domains.Commands.SetEmailForwarding;

/// <summary>Command to toggle email forwarding on a domain.</summary>
/// <param name="DomainId">Domain primary key.</param>
/// <param name="Enabled">Whether email forwarding should be enabled.</param>
public record SetEmailForwardingCommand(int DomainId, bool Enabled);
```

```csharp
// SetEmailForwardingHandler.cs
namespace Innovayse.Application.Domains.Commands.SetEmailForwarding;

using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Common;

/// <summary>Handles <see cref="SetEmailForwardingCommand"/>.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="registrar">Registrar provider.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class SetEmailForwardingHandler(IDomainRepository repo, IRegistrarProvider registrar, IUnitOfWork uow)
{
    /// <summary>
    /// Toggles email forwarding at the registrar and on the domain entity.
    /// </summary>
    /// <param name="command">The toggle command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(SetEmailForwardingCommand command, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(command.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain with ID {command.DomainId} not found.");

        var result = await registrar.SetEmailForwardingAsync(domain.Name, command.Enabled, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to update email forwarding at registrar.");
        }

        domain.SetEmailForwarding(command.Enabled);
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Application/Domains/Commands/UpdateDomain/ \
       backend/src/Innovayse.Application/Domains/Commands/ModifyDomainContact/ \
       backend/src/Innovayse.Application/Domains/Commands/SetDnsManagement/ \
       backend/src/Innovayse.Application/Domains/Commands/SetEmailForwarding/
git commit -m "feat(app): add UpdateDomain, ModifyContact, SetDnsManagement, SetEmailForwarding commands"
```

---

## Task 8: Backend — Email Forwarding Rule Commands

**Files:**
- Create: `backend/src/Innovayse.Application/Domains/Commands/AddEmailForwardingRule/AddEmailForwardingRuleCommand.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/AddEmailForwardingRule/AddEmailForwardingRuleHandler.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/UpdateEmailForwardingRule/UpdateEmailForwardingRuleCommand.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/UpdateEmailForwardingRule/UpdateEmailForwardingRuleHandler.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/DeleteEmailForwardingRule/DeleteEmailForwardingRuleCommand.cs`
- Create: `backend/src/Innovayse.Application/Domains/Commands/DeleteEmailForwardingRule/DeleteEmailForwardingRuleHandler.cs`

- [ ] **Step 1: Create AddEmailForwardingRuleCommand + Handler**

Follow the exact pattern of AddDnsRecordCommand/Handler:

```csharp
// AddEmailForwardingRuleCommand.cs
namespace Innovayse.Application.Domains.Commands.AddEmailForwardingRule;

/// <summary>Command to add an email forwarding rule to a domain.</summary>
/// <param name="DomainId">Domain primary key.</param>
/// <param name="Source">Source alias (e.g. "info", "support").</param>
/// <param name="Destination">Destination email address.</param>
public record AddEmailForwardingRuleCommand(int DomainId, string Source, string Destination);
```

```csharp
// AddEmailForwardingRuleHandler.cs
namespace Innovayse.Application.Domains.Commands.AddEmailForwardingRule;

using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Common;

/// <summary>Handles <see cref="AddEmailForwardingRuleCommand"/>.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="registrar">Registrar provider.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class AddEmailForwardingRuleHandler(IDomainRepository repo, IRegistrarProvider registrar, IUnitOfWork uow)
{
    /// <summary>
    /// Adds a forwarding rule to the domain aggregate and syncs to the registrar.
    /// </summary>
    /// <param name="command">The add rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(AddEmailForwardingRuleCommand command, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(command.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain with ID {command.DomainId} not found.");

        domain.AddForwardingRule(command.Source, command.Destination);
        await uow.SaveChangesAsync(ct);

        await registrar.AddEmailForwardingRuleAsync(domain.Name, command.Source, command.Destination, ct);
    }
}
```

- [ ] **Step 2: Create UpdateEmailForwardingRuleCommand + Handler**

```csharp
// UpdateEmailForwardingRuleCommand.cs
namespace Innovayse.Application.Domains.Commands.UpdateEmailForwardingRule;

/// <summary>Command to update an email forwarding rule.</summary>
/// <param name="DomainId">Domain primary key.</param>
/// <param name="RuleId">Forwarding rule primary key.</param>
/// <param name="Source">Updated source alias.</param>
/// <param name="Destination">Updated destination email.</param>
/// <param name="IsActive">Whether the rule should be active.</param>
public record UpdateEmailForwardingRuleCommand(int DomainId, int RuleId, string Source, string Destination, bool IsActive);
```

```csharp
// UpdateEmailForwardingRuleHandler.cs
namespace Innovayse.Application.Domains.Commands.UpdateEmailForwardingRule;

using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Common;

/// <summary>Handles <see cref="UpdateEmailForwardingRuleCommand"/>.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="registrar">Registrar provider.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class UpdateEmailForwardingRuleHandler(IDomainRepository repo, IRegistrarProvider registrar, IUnitOfWork uow)
{
    /// <summary>
    /// Updates the forwarding rule on the aggregate and syncs to the registrar.
    /// </summary>
    /// <param name="command">The update rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(UpdateEmailForwardingRuleCommand command, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(command.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain with ID {command.DomainId} not found.");

        domain.UpdateForwardingRule(command.RuleId, command.Source, command.Destination, command.IsActive);
        await uow.SaveChangesAsync(ct);

        await registrar.UpdateEmailForwardingRuleAsync(domain.Name, command.Source, command.Destination, ct);
    }
}
```

- [ ] **Step 3: Create DeleteEmailForwardingRuleCommand + Handler**

```csharp
// DeleteEmailForwardingRuleCommand.cs
namespace Innovayse.Application.Domains.Commands.DeleteEmailForwardingRule;

/// <summary>Command to delete an email forwarding rule from a domain.</summary>
/// <param name="DomainId">Domain primary key.</param>
/// <param name="RuleId">Forwarding rule primary key.</param>
public record DeleteEmailForwardingRuleCommand(int DomainId, int RuleId);
```

```csharp
// DeleteEmailForwardingRuleHandler.cs
namespace Innovayse.Application.Domains.Commands.DeleteEmailForwardingRule;

using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Common;

/// <summary>Handles <see cref="DeleteEmailForwardingRuleCommand"/>.</summary>
/// <param name="repo">Domain repository.</param>
/// <param name="registrar">Registrar provider.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class DeleteEmailForwardingRuleHandler(IDomainRepository repo, IRegistrarProvider registrar, IUnitOfWork uow)
{
    /// <summary>
    /// Removes the forwarding rule from the aggregate and deletes at the registrar.
    /// </summary>
    /// <param name="command">The delete rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(DeleteEmailForwardingRuleCommand command, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(command.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain with ID {command.DomainId} not found.");

        var rule = domain.EmailForwardingRules.FirstOrDefault(r => r.Id == command.RuleId)
            ?? throw new InvalidOperationException($"Email forwarding rule with ID {command.RuleId} not found.");

        var source = rule.Source;

        domain.RemoveForwardingRule(command.RuleId);
        await uow.SaveChangesAsync(ct);

        await registrar.DeleteEmailForwardingRuleAsync(domain.Name, source, ct);
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.Application/Domains/Commands/AddEmailForwardingRule/ \
       backend/src/Innovayse.Application/Domains/Commands/UpdateEmailForwardingRule/ \
       backend/src/Innovayse.Application/Domains/Commands/DeleteEmailForwardingRule/
git commit -m "feat(app): add email forwarding rule CRUD commands"
```

---

## Task 9: Backend — API Endpoints

**Files:**
- Modify: `backend/src/Innovayse.API/Domains/DomainsController.cs`
- Create: `backend/src/Innovayse.API/Domains/Requests/UpdateDomainRequest.cs`
- Create: `backend/src/Innovayse.API/Domains/Requests/ModifyContactRequest.cs`
- Create: `backend/src/Innovayse.API/Domains/Requests/EmailForwardingRuleRequest.cs`

- [ ] **Step 1: Create API request models**

```csharp
// UpdateDomainRequest.cs
namespace Innovayse.API.Domains.Requests;

/// <summary>Request body for updating editable domain fields.</summary>
public sealed class UpdateDomainRequest
{
    /// <summary>One-time registration cost.</summary>
    public required decimal FirstPaymentAmount { get; init; }
    /// <summary>Recurring renewal amount.</summary>
    public required decimal RecurringAmount { get; init; }
    /// <summary>Payment method label.</summary>
    public string? PaymentMethod { get; init; }
    /// <summary>Applied promotion code.</summary>
    public string? PromotionCode { get; init; }
    /// <summary>External subscription reference.</summary>
    public string? SubscriptionId { get; init; }
    /// <summary>Free-text admin notes.</summary>
    public string? AdminNotes { get; init; }
    /// <summary>Expiry date (ISO 8601).</summary>
    public required string ExpiresAt { get; init; }
    /// <summary>Next due date (ISO 8601).</summary>
    public required string NextDueDate { get; init; }
    /// <summary>Registration period in years.</summary>
    public required int RegistrationPeriod { get; init; }
    /// <summary>Domain lifecycle status.</summary>
    public required string Status { get; init; }
    /// <summary>Nameserver hostnames (up to 5).</summary>
    public List<string> Nameservers { get; init; } = [];
}
```

```csharp
// ModifyContactRequest.cs
namespace Innovayse.API.Domains.Requests;

/// <summary>Request body for modifying WHOIS registrant contact details.</summary>
public sealed class ModifyContactRequest
{
    /// <summary>Registrant first name.</summary>
    public required string FirstName { get; init; }
    /// <summary>Registrant last name.</summary>
    public required string LastName { get; init; }
    /// <summary>Organization name.</summary>
    public string? Organization { get; init; }
    /// <summary>Contact email.</summary>
    public required string Email { get; init; }
    /// <summary>Contact phone.</summary>
    public required string Phone { get; init; }
    /// <summary>Street address.</summary>
    public required string Address1 { get; init; }
    /// <summary>Secondary address line.</summary>
    public string? Address2 { get; init; }
    /// <summary>City.</summary>
    public required string City { get; init; }
    /// <summary>State or province.</summary>
    public required string State { get; init; }
    /// <summary>Postal code.</summary>
    public required string PostalCode { get; init; }
    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public required string Country { get; init; }
}
```

```csharp
// EmailForwardingRuleRequest.cs
namespace Innovayse.API.Domains.Requests;

/// <summary>Request body for creating or updating an email forwarding rule.</summary>
public sealed class EmailForwardingRuleRequest
{
    /// <summary>Source alias (e.g. "info", "support").</summary>
    public required string Source { get; init; }
    /// <summary>Destination email address.</summary>
    public required string Destination { get; init; }
    /// <summary>Whether the rule is active (used in updates only).</summary>
    public bool IsActive { get; init; } = true;
}
```

- [ ] **Step 2: Add new endpoints to DomainsController**

Add these endpoints after the existing methods. Update the `ListDomainsAsync` method to accept an optional `clientId` query parameter and pass it to `ListDomainsQuery`:

```csharp
// Update existing ListDomainsAsync signature:
public async Task<ActionResult<PagedResult<DomainRegistrationDto>>> ListDomainsAsync(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] int? clientId = null,
    CancellationToken ct = default)
{
    var result = await bus.InvokeAsync<PagedResult<DomainRegistrationDto>>(
        new ListDomainsQuery(page, pageSize, clientId), ct);
    return Ok(result);
}

// --- New endpoints ---

/// <summary>Updates editable domain fields.</summary>
[HttpPut("{id:int}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> UpdateAsync(int id, UpdateDomainRequest req, CancellationToken ct)
{
    await bus.InvokeAsync(new UpdateDomainCommand(
        id, req.FirstPaymentAmount, req.RecurringAmount, req.PaymentMethod,
        req.PromotionCode, req.SubscriptionId, req.AdminNotes,
        req.ExpiresAt, req.NextDueDate, req.RegistrationPeriod, req.Status,
        req.Nameservers), ct);
    return NoContent();
}

/// <summary>Modifies WHOIS registrant contact details at the registrar.</summary>
[HttpPost("{id:int}/modify-contact")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> ModifyContactAsync(int id, ModifyContactRequest req, CancellationToken ct)
{
    await bus.InvokeAsync(new ModifyDomainContactCommand(id, new DomainContact(
        req.FirstName, req.LastName, req.Organization, req.Email, req.Phone,
        req.Address1, req.Address2, req.City, req.State, req.PostalCode, req.Country)), ct);
    return NoContent();
}

/// <summary>Toggles DNS management for a domain.</summary>
[HttpPut("{id:int}/dns-management")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> SetDnsManagementAsync(int id, SetBoolRequest req, CancellationToken ct)
{
    await bus.InvokeAsync(new SetDnsManagementCommand(id, req.Enabled), ct);
    return NoContent();
}

/// <summary>Toggles email forwarding for a domain.</summary>
[HttpPut("{id:int}/email-forwarding-toggle")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> SetEmailForwardingAsync(int id, SetBoolRequest req, CancellationToken ct)
{
    await bus.InvokeAsync(new SetEmailForwardingCommand(id, req.Enabled), ct);
    return NoContent();
}

/// <summary>Adds an email forwarding rule.</summary>
[HttpPost("{id:int}/email-forwarding")]
[ProducesResponseType(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> AddEmailForwardingRuleAsync(int id, EmailForwardingRuleRequest req, CancellationToken ct)
{
    await bus.InvokeAsync(new AddEmailForwardingRuleCommand(id, req.Source, req.Destination), ct);
    return Created($"/api/domains/{id}", null);
}

/// <summary>Updates an email forwarding rule.</summary>
[HttpPut("{id:int}/email-forwarding/{ruleId:int}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> UpdateEmailForwardingRuleAsync(int id, int ruleId, EmailForwardingRuleRequest req, CancellationToken ct)
{
    await bus.InvokeAsync(new UpdateEmailForwardingRuleCommand(id, ruleId, req.Source, req.Destination, req.IsActive), ct);
    return NoContent();
}

/// <summary>Deletes an email forwarding rule.</summary>
[HttpDelete("{id:int}/email-forwarding/{ruleId:int}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DeleteEmailForwardingRuleAsync(int id, int ruleId, CancellationToken ct)
{
    await bus.InvokeAsync(new DeleteEmailForwardingRuleCommand(id, ruleId), ct);
    return NoContent();
}
```

Add the necessary using directives at the top of the controller file.

- [ ] **Step 3: Commit**

```bash
git add backend/src/Innovayse.API/Domains/
git commit -m "feat(api): add domain update, contact modify, email forwarding, DNS management endpoints"
```

---

## Task 10: Backend — Build & Migrate

- [ ] **Step 1: Build the solution to verify compilation**

```bash
cd backend && dotnet build
```

Fix any compilation errors (likely in RegisterDomainHandler where `Price` was renamed to `RecurringAmount`, and anywhere that constructs a `DomainDto`).

- [ ] **Step 2: Apply migration**

```bash
cd backend/src/Innovayse.API && dotnet ef database update
```

- [ ] **Step 3: Commit any build fixes**

```bash
git add -A backend/
git commit -m "fix: resolve compilation errors from Price→RecurringAmount rename and new DTO fields"
```

---

## Task 11: Frontend — Types & Constants

**Files:**
- Modify: `admin/src/types/models.ts`
- Modify: `admin/src/utils/constants.ts`

- [ ] **Step 1: Add new types to models.ts**

Add after the existing `DomainRegistration` interface:

```typescript
/** Full domain detail returned by GET /api/domains/{id}. */
export interface DomainDetail {
  /** Unique domain identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Fully-qualified domain name. */
  name: string
  /** Top-level domain suffix. */
  tld: string
  /** Current lifecycle status. */
  status: string
  /** ISO 8601 registration date. */
  registeredAt: string
  /** ISO 8601 expiry date. */
  expiresAt: string
  /** Whether auto-renew is enabled. */
  autoRenew: boolean
  /** Whether WHOIS privacy is enabled. */
  whoisPrivacy: boolean
  /** Whether registrar transfer lock is enabled. */
  isLocked: boolean
  /** Registrar-assigned reference. */
  registrarRef: string | null
  /** EPP authorization code. */
  eppCode: string | null
  /** Linked hosting service ID. */
  linkedServiceId: number | null
  /** One-time registration cost. */
  firstPaymentAmount: number
  /** Recurring renewal amount. */
  recurringAmount: number
  /** ISO 4217 currency code. */
  priceCurrency: string
  /** ISO 8601 next due date. */
  nextDueDate: string
  /** Registrar module name. */
  registrar: string | null
  /** Registration period in years. */
  registrationPeriod: number
  /** Payment method label. */
  paymentMethod: string | null
  /** Applied promotion code. */
  promotionCode: string | null
  /** External subscription reference. */
  subscriptionId: string | null
  /** Free-text admin notes. */
  adminNotes: string | null
  /** Order ID that created this domain. */
  orderId: number | null
  /** Order type: "Register" or "Transfer". */
  orderType: string
  /** Whether DNS management is enabled. */
  dnsManagement: boolean
  /** Whether email forwarding is enabled. */
  emailForwarding: boolean
  /** Nameserver list. */
  nameservers: NameserverItem[]
  /** DNS records. */
  dnsRecords: DnsRecordItem[]
  /** Email forwarding rules. */
  emailForwardingRules: EmailForwardingRuleItem[]
  /** Domain reminder history. */
  reminders: DomainReminderItem[]
}

/** Nameserver DTO. */
export interface NameserverItem {
  /** Nameserver primary key. */
  id: number
  /** Nameserver hostname. */
  host: string
}

/** DNS record DTO. */
export interface DnsRecordItem {
  /** Record primary key. */
  id: number
  /** Record type (A, AAAA, CNAME, MX, TXT, NS, SRV). */
  type: string
  /** Record host/name. */
  host: string
  /** Record value. */
  value: string
  /** Time-to-live in seconds. */
  ttl: number
  /** Priority (MX/SRV only). */
  priority: number | null
}

/** Email forwarding rule DTO. */
export interface EmailForwardingRuleItem {
  /** Rule primary key. */
  id: number
  /** Source alias. */
  source: string
  /** Destination email address. */
  destination: string
  /** Whether the rule is active. */
  isActive: boolean
}

/** Domain reminder history DTO. */
export interface DomainReminderItem {
  /** Reminder primary key. */
  id: number
  /** Reminder type label. */
  reminderType: string
  /** Recipient email. */
  sentTo: string
  /** ISO 8601 sent timestamp. */
  sentAt: string
}
```

- [ ] **Step 2: Add domain status styles and options to constants.ts**

```typescript
/** Status badge styles for domains. */
export const DOMAIN_STATUS_STYLES: Record<string, string> = {
  Active: 'text-status-green bg-status-green/10 border-status-green/20',
  PendingRegistration: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  PendingTransfer: 'text-primary-400 bg-primary-500/10 border-primary-500/20',
  Expired: 'text-status-red bg-status-red/10 border-status-red/20',
  Redemption: 'text-status-yellow bg-status-yellow/10 border-status-yellow/20',
  Transferred: 'text-text-muted bg-white/[0.04] border-border',
  Cancelled: 'text-text-muted bg-white/[0.04] border-border',
}

/** Domain status options for dropdowns. */
export const DOMAIN_STATUS_OPTIONS = [
  { value: 'Active', label: 'Active' },
  { value: 'PendingRegistration', label: 'Pending Registration' },
  { value: 'PendingTransfer', label: 'Pending Transfer' },
  { value: 'Expired', label: 'Expired' },
  { value: 'Redemption', label: 'Redemption' },
  { value: 'Transferred', label: 'Transferred' },
  { value: 'Cancelled', label: 'Cancelled' },
]

/** Payment method options for dropdowns. */
export const PAYMENT_METHOD_OPTIONS = [
  { value: '', label: 'None' },
  { value: 'Credit/Debit Card', label: 'Credit/Debit Card' },
  { value: 'Bank Transfer', label: 'Bank Transfer' },
  { value: 'PayPal', label: 'PayPal' },
]

/** DNS record type options for dropdowns. */
export const DNS_RECORD_TYPE_OPTIONS = [
  { value: 'A', label: 'A' },
  { value: 'AAAA', label: 'AAAA' },
  { value: 'CNAME', label: 'CNAME' },
  { value: 'MX', label: 'MX' },
  { value: 'TXT', label: 'TXT' },
  { value: 'NS', label: 'NS' },
  { value: 'SRV', label: 'SRV' },
]
```

- [ ] **Step 3: Commit**

```bash
git add admin/src/types/models.ts admin/src/utils/constants.ts
git commit -m "feat(admin): add domain detail types and status constants"
```

---

## Task 12: Frontend — Domains Store

**Files:**
- Modify: `admin/src/modules/domains/stores/domainsStore.ts`

- [ ] **Step 1: Extend the store with all domain actions**

Rewrite the store to add detail state and all CRUD actions. Keep the existing `fetchAll` method and add new ones. Use the existing `useApi` composable pattern:

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { DomainRegistration, DomainDetail, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin domains management.
 */
export const useDomainsStore = defineStore('domains', () => {
  const { request } = useApi()

  /** Loaded domain registrations list. */
  const domains = ref<DomainRegistration[]>([])

  /** Domains for a specific client. */
  const clientDomains = ref<DomainRegistration[]>([])

  /** Currently loaded domain detail. */
  const current = ref<DomainDetail | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Total number of matching items. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches paginated domain registrations from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<DomainRegistration>>(`/domains?page=${page.value}&pageSize=${pageSize.value}`)
      domains.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load domains.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches domains for a specific client.
   *
   * @param clientId - The client identifier to filter by.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchByClient(clientId: number | string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<DomainRegistration>>(
        `/domains?clientId=${clientId}&page=${page.value}&pageSize=${pageSize.value}`,
      )
      clientDomains.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load client domains.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single domain by ID.
   *
   * @param id - Domain primary key.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchById(id: number | string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      current.value = await request<DomainDetail>(`/domains/${id}`)
    } catch {
      error.value = 'Failed to load domain details.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates editable domain fields.
   *
   * @param id - Domain primary key.
   * @param data - Updated field values.
   * @returns Promise that resolves when the update completes.
   */
  async function update(id: number | string, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}`, { method: 'PUT', body: JSON.stringify(data) })
  }

  /**
   * Renews a domain for additional years.
   *
   * @param id - Domain primary key.
   * @param years - Number of years to renew.
   * @returns Promise that resolves when renewal completes.
   */
  async function renew(id: number | string, years: number): Promise<void> {
    await request(`/domains/${id}/renew`, { method: 'POST', body: JSON.stringify({ years }) })
  }

  /**
   * Toggles auto-renew for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether auto-renew should be enabled.
   */
  async function setAutoRenew(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/auto-renew`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles WHOIS privacy for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether WHOIS privacy should be enabled.
   */
  async function setWhoisPrivacy(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/whois-privacy`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles registrar lock for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether registrar lock should be enabled.
   */
  async function setLock(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/lock`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles DNS management for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether DNS management should be enabled.
   */
  async function setDnsManagement(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/dns-management`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Toggles email forwarding for a domain.
   *
   * @param id - Domain primary key.
   * @param enabled - Whether email forwarding should be enabled.
   */
  async function setEmailForwarding(id: number | string, enabled: boolean): Promise<void> {
    await request(`/domains/${id}/email-forwarding-toggle`, { method: 'PUT', body: JSON.stringify({ enabled }) })
  }

  /**
   * Modifies WHOIS registrant contact details.
   *
   * @param id - Domain primary key.
   * @param contact - Updated contact details.
   */
  async function modifyContact(id: number | string, contact: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/modify-contact`, { method: 'POST', body: JSON.stringify(contact) })
  }

  /**
   * Initiates an outgoing transfer and retrieves the EPP code.
   *
   * @param id - Domain primary key.
   * @returns The EPP authorization code.
   */
  async function getEppCode(id: number | string): Promise<string> {
    return await request<string>(`/domains/${id}/initiate-outgoing-transfer`, { method: 'POST' })
  }

  /**
   * Adds a DNS record to a domain.
   *
   * @param id - Domain primary key.
   * @param data - DNS record details.
   */
  async function addDnsRecord(id: number | string, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/dns`, { method: 'POST', body: JSON.stringify(data) })
  }

  /**
   * Updates a DNS record.
   *
   * @param id - Domain primary key.
   * @param recordId - DNS record primary key.
   * @param data - Updated record details.
   */
  async function updateDnsRecord(id: number | string, recordId: number, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/dns/${recordId}`, { method: 'PUT', body: JSON.stringify(data) })
  }

  /**
   * Deletes a DNS record.
   *
   * @param id - Domain primary key.
   * @param recordId - DNS record primary key.
   */
  async function deleteDnsRecord(id: number | string, recordId: number): Promise<void> {
    await request(`/domains/${id}/dns/${recordId}`, { method: 'DELETE' })
  }

  /**
   * Adds an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param source - Source alias.
   * @param destination - Destination email address.
   */
  async function addForwardingRule(id: number | string, source: string, destination: string): Promise<void> {
    await request(`/domains/${id}/email-forwarding`, { method: 'POST', body: JSON.stringify({ source, destination }) })
  }

  /**
   * Updates an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param ruleId - Forwarding rule primary key.
   * @param data - Updated rule details.
   */
  async function updateForwardingRule(id: number | string, ruleId: number, data: Record<string, unknown>): Promise<void> {
    await request(`/domains/${id}/email-forwarding/${ruleId}`, { method: 'PUT', body: JSON.stringify(data) })
  }

  /**
   * Deletes an email forwarding rule.
   *
   * @param id - Domain primary key.
   * @param ruleId - Forwarding rule primary key.
   */
  async function deleteForwardingRule(id: number | string, ruleId: number): Promise<void> {
    await request(`/domains/${id}/email-forwarding/${ruleId}`, { method: 'DELETE' })
  }

  return {
    domains, clientDomains, current, page, pageSize, totalCount, loading, error,
    fetchAll, fetchByClient, fetchById, update, renew,
    setAutoRenew, setWhoisPrivacy, setLock, setDnsManagement, setEmailForwarding,
    modifyContact, getEppCode,
    addDnsRecord, updateDnsRecord, deleteDnsRecord,
    addForwardingRule, updateForwardingRule, deleteForwardingRule,
  }
})
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/domains/stores/domainsStore.ts
git commit -m "feat(admin): extend domains store with detail, client filter, and CRUD actions"
```

---

## Task 13: Frontend — Router & Inner Sidebar

**Files:**
- Modify: `admin/src/router/index.ts`
- Modify: `admin/src/modules/clients/components/ClientInnerSidebar.vue`

- [ ] **Step 1: Add domain routes**

In the `clients/:id` children array, add after the `contacts` route:

```typescript
{ path: 'domains', component: () => import('../modules/clients/views/ClientDomainsListView.vue') },
{ path: 'domains/:domainId', component: () => import('../modules/clients/views/ClientDomainDetailView.vue') },
```

- [ ] **Step 2: Add Domains to inner sidebar**

In `ClientInnerSidebar.vue`, add a new nav item in the `navItems` computed array, after "Products/Services" and before "Users":

```typescript
{
  icon: 'domains',
  label: 'Domains',
  to: `/clients/${props.clientId}/domains`,
},
```

Add the globe SVG icon in the template's icon rendering section:

```html
<!-- Domains icon -->
<svg v-else-if="item.icon === 'domains'" class="w-5 h-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
  <circle cx="12" cy="12" r="10"/>
  <line x1="2" y1="12" x2="22" y2="12"/>
  <path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z"/>
</svg>
```

- [ ] **Step 3: Commit**

```bash
git add admin/src/router/index.ts admin/src/modules/clients/components/ClientInnerSidebar.vue
git commit -m "feat(admin): add Domains to inner sidebar and router"
```

---

## Task 14: Frontend — ClientDomainsListView.vue

**Files:**
- Create: `admin/src/modules/clients/views/ClientDomainsListView.vue`

- [ ] **Step 1: Create the domains list view**

Follow the exact pattern of `ClientServicesListView.vue`. Use `useApi` directly (not via the store) for the client-filtered request, matching the services list pattern. Reuse `DOMAIN_STATUS_STYLES` from constants, `formatDate` from format.ts.

The component should have:
- Paginated table with columns: ID, Domain Name, Registrar, Status, Expiry Date, Auto-Renew
- Status badges using `DOMAIN_STATUS_STYLES`
- Row click navigates to `/clients/:id/domains/:domainId`
- Loading, error, and empty states matching the service list pattern
- Reuse `formatDate` for dates

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/clients/views/ClientDomainsListView.vue
git commit -m "feat(admin): add client domains list view"
```

---

## Task 15: Frontend — Sub-components (DNS, Email Forwarding, Reminders, Contact Modal)

**Files:**
- Create: `admin/src/modules/clients/components/DnsRecordsTable.vue`
- Create: `admin/src/modules/clients/components/EmailForwardingTable.vue`
- Create: `admin/src/modules/clients/components/DomainRemindersTable.vue`
- Create: `admin/src/modules/clients/components/DomainContactModal.vue`

- [ ] **Step 1: Create DnsRecordsTable.vue**

Inline CRUD table component. Props: `domainId` (number), `records` (DnsRecordItem[]). Emits: `add`, `update`, `delete`, `refresh`.

Uses `AppSelect` for record type dropdown (reuse `DNS_RECORD_TYPE_OPTIONS` from constants). Shows Priority field only for MX/SRV types. Inline add/edit rows. Delete with `confirm()`.

- [ ] **Step 2: Create EmailForwardingTable.vue**

Inline CRUD table component. Props: `domainId` (number), `rules` (EmailForwardingRuleItem[]). Emits: `add`, `update`, `delete`, `refresh`.

Inline add/edit rows with Source and Destination inputs. Active toggle using `ToggleSwitch` component. Delete with `confirm()`.

- [ ] **Step 3: Create DomainRemindersTable.vue**

Read-only table component. Props: `reminders` (DomainReminderItem[]). No emits needed.

Columns: Date, Reminder, To, Sent. Uses `formatDate` for timestamps. Empty state: "No Records Found".

- [ ] **Step 4: Create DomainContactModal.vue**

Modal component following `ContactFormModal.vue` pattern. Props: `saving` (boolean). Emits: `save` (contact data), `close`.

Two-column form with all DomainContact fields. Uses existing modal pattern (fixed inset-0, backdrop blur, rounded card). Reuse `useGeoOptions` composable for country dropdown if available, otherwise plain text input.

- [ ] **Step 5: Commit**

```bash
git add admin/src/modules/clients/components/DnsRecordsTable.vue \
       admin/src/modules/clients/components/EmailForwardingTable.vue \
       admin/src/modules/clients/components/DomainRemindersTable.vue \
       admin/src/modules/clients/components/DomainContactModal.vue
git commit -m "feat(admin): add DNS, email forwarding, reminders, and contact modal components"
```

---

## Task 16: Frontend — ClientDomainDetailView.vue

**Files:**
- Create: `admin/src/modules/clients/views/ClientDomainDetailView.vue`

- [ ] **Step 1: Create the domain detail view**

Follow the exact pattern of `ClientServiceDetailView.vue`:

- `useRoute` for params, `useRouter` for back navigation
- `useApi` for direct API calls (matching service detail pattern)
- Form refs for all editable fields, `populateForm()` function
- `handleSave()` sends PUT to `/domains/{id}`
- `handleCancel()` resets form
- Success/error feedback with 3000ms auto-dismiss

Layout sections:
1. **Action bar** (top) — Back button, domain name, Cancel + Save Changes buttons
2. **Two-column form** — left column (order info, amounts, promo, nameservers), right column (dates, payment method, status)
3. **Registrar Commands** — action buttons (Register, Renew, Modify Contact, Get EPP Code, ID Protection toggle)
4. **Management Tools** — `ToggleSwitch` components for DNS Management, Email Forwarding, ID Protection, Auto Renew, Registrar Lock
5. **Email Forwarding Rules** — `EmailForwardingTable` (v-if emailForwarding enabled)
6. **DNS Records** — `DnsRecordsTable` (v-if dnsManagement enabled)
7. **Domain Reminder History** — `DomainRemindersTable`
8. **Admin Notes** — textarea

Reuse:
- `AppSelect` for Status and Payment Method dropdowns
- `ToggleSwitch` for all management toggles
- `formatDate` and `toDateInputValue` from format.ts
- `DOMAIN_STATUS_OPTIONS` and `PAYMENT_METHOD_OPTIONS` from constants.ts
- Same Tailwind classes as `ClientServiceDetailView.vue` (input styles, card styles, section headers)

For registrar commands:
- Register button: disabled when status is Active
- Renew button: opens `prompt()` for years input, calls store `renew()`
- Modify Contact: toggles `DomainContactModal` visibility
- Get EPP Code: calls store `getEppCode()`, shows result in `alert()`
- ID Protection: calls store `setWhoisPrivacy()` inline

For management toggles: each toggle calls the respective store action and re-fetches domain detail.

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/clients/views/ClientDomainDetailView.vue
git commit -m "feat(admin): add domain detail view with full WHMCS-style layout"
```

---

## Task 17: Frontend — Build & Verify

- [ ] **Step 1: Build the admin frontend**

```bash
cd admin && npx vite build
```

Fix any TypeScript or build errors.

- [ ] **Step 2: Commit any build fixes**

```bash
git add -A admin/
git commit -m "fix(admin): resolve build errors in domain detail feature"
```

---

## Task 18: Integration Test

- [ ] **Step 1: Start the API and admin dev server**

```bash
# In separate terminals or via docker-compose
docker compose up -d
```

- [ ] **Step 2: Manual verification checklist**

1. Navigate to a client → inner sidebar shows "Domains" item with globe icon
2. Click Domains → empty state shows if no domains exist
3. If domains exist: table shows ID, Domain Name, Registrar, Status, Expiry, Auto-Renew
4. Click a domain row → domain detail page loads
5. Verify all fields populate correctly (order info, dates, amounts, nameservers)
6. Edit fields and click Save Changes → success feedback appears
7. Click Cancel → form resets to saved values
8. Toggle management tools (DNS Management, Email Forwarding, etc.) → toggles persist
9. When DNS Management is ON → DNS Records table appears
10. When Email Forwarding is ON → Email Forwarding Rules table appears
11. Domain Reminder History table shows (empty is fine for now)
12. Admin Notes textarea saves with the main form

- [ ] **Step 3: Final commit**

```bash
git add -A
git commit -m "feat: complete admin domain detail page with full WHMCS layout"
```
