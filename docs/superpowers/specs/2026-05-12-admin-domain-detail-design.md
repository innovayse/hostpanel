# Admin Domain Detail Page — Design Spec

**Date:** 2026-05-12
**Status:** Approved

## Overview

Add a full domain detail sub-page to the admin client profile (inner sidebar), matching the WHMCS domain management page. Includes editable domain fields, registrar commands, management tool toggles, email forwarding rules, DNS record management, domain reminder history, and admin notes.

## Scope

- Backend: new entity fields, new entities, new commands/queries, new API endpoints
- Frontend: inner sidebar item, domains list per client, domain detail page with extracted sub-components

## Future Enhancement (Out of Scope)

- **Recalculate on Save**: toggle that auto-recalculates recurring amount from TLD pricing table + promotion code. Requires TLD pricing infrastructure. Will be added later.

---

## 1. Backend — Domain Entity Changes

### 1.1 New Properties on `Domain` Aggregate

| Property | Type | Default | Purpose |
|---|---|---|---|
| `FirstPaymentAmount` | `decimal` | `0` | One-time registration cost |
| `PaymentMethod` | `string?` | `null` | e.g. "Credit/Debit Card", "Bank Transfer" |
| `PromotionCode` | `string?` | `null` | Applied promo/coupon code |
| `SubscriptionId` | `string?` | `null` | External payment subscription reference |
| `AdminNotes` | `string?` | `null` | Free-text admin notes |
| `OrderId` | `int?` | `null` | FK to the order that created this domain |
| `OrderType` | `string` | `"Register"` | "Register" or "Transfer" |
| `DnsManagement` | `bool` | `false` | Whether DNS management is enabled |
| `EmailForwarding` | `bool` | `false` | Whether email forwarding is enabled |

### 1.2 Renamed Property

- `Price` → `RecurringAmount` (rename to match WHMCS terminology; existing `Price` column renamed via migration)

### 1.3 New Collections

- `_emailForwardingRules` → `IReadOnlyList<EmailForwardingRule>`
- `_reminders` → `IReadOnlyList<DomainReminder>`

### 1.4 New Domain Methods

- `Update(...)` — updates editable fields (amounts, dates, status, payment method, promo code, subscription ID, notes, registration period)
- `SetDnsManagement(bool)` — enables/disables DNS management
- `SetEmailForwarding(bool)` — enables/disables email forwarding
- `AddForwardingRule(string source, string destination)` — adds email forwarding rule
- `UpdateForwardingRule(int ruleId, string source, string destination, bool isActive)` — updates existing rule
- `RemoveForwardingRule(int ruleId)` — removes forwarding rule by ID
- `AddReminder(string reminderType, string sentTo)` — logs a reminder (called by notification system)

### 1.5 Factory Method Updates

- `Register(...)` — accept `firstPaymentAmount`, set `OrderType = "Register"`
- `CreateTransfer(...)` — set `OrderType = "Transfer"`

---

## 2. Backend — New Entities

### 2.1 EmailForwardingRule (Entity, owned by Domain)

| Property | Type | Purpose |
|---|---|---|
| `Id` | `int` | PK |
| `DomainId` | `int` | FK to Domain |
| `Source` | `string` | Local part or alias (e.g. "info", "support") |
| `Destination` | `string` | Target email address |
| `IsActive` | `bool` | Whether this rule is currently active |

**File:** `Domain/Domains/EmailForwardingRule.cs`
**EF Config:** `Infrastructure/Domains/Configurations/EmailForwardingRuleConfiguration.cs`
- Table: `email_forwarding_rules`
- Max lengths: Source 255, Destination 320
- Cascade delete on domain
- Index on DomainId

### 2.2 DomainReminder (Entity, owned by Domain)

| Property | Type | Purpose |
|---|---|---|
| `Id` | `int` | PK |
| `DomainId` | `int` | FK to Domain |
| `ReminderType` | `string` | e.g. "30 Days Before Expiry", "7 Days Before Expiry", "Domain Expired" |
| `SentTo` | `string` | Recipient email address |
| `SentAt` | `DateTimeOffset` | Timestamp when the reminder was sent |

**File:** `Domain/Domains/DomainReminder.cs`
**EF Config:** `Infrastructure/Domains/Configurations/DomainReminderConfiguration.cs`
- Table: `domain_reminders`
- Read-only from admin perspective (created by notification system)
- Max lengths: ReminderType 100, SentTo 320
- Cascade delete on domain
- Index on DomainId

### 2.3 DomainContact (Value Object / Record)

Not persisted — used as a parameter for the Modify Contact Details registrar call.

| Field | Type |
|---|---|
| `FirstName` | `string` |
| `LastName` | `string` |
| `Organization` | `string?` |
| `Email` | `string` |
| `Phone` | `string` |
| `Address1` | `string` |
| `Address2` | `string?` |
| `City` | `string` |
| `State` | `string` |
| `PostalCode` | `string` |
| `Country` | `string` |

**File:** `Domain/Domains/DomainContact.cs`

---

## 3. Backend — IRegistrarProvider Changes

Add to the existing `IRegistrarProvider` interface:

```csharp
Task<RegistrarResult> ModifyContactDetailsAsync(string domainName, DomainContact contact, CancellationToken ct);
Task<RegistrarResult> SetEmailForwardingAsync(string domainName, bool enabled, CancellationToken ct);
Task<RegistrarResult> AddEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct);
Task<RegistrarResult> UpdateEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct);
Task<RegistrarResult> DeleteEmailForwardingRuleAsync(string domainName, string source, CancellationToken ct);
Task<RegistrarResult> SetDnsManagementAsync(string domainName, bool enabled, CancellationToken ct);
```

Update `NamecheapRegistrarProvider` with stub implementations that return success (actual Namecheap API integration later).

---

## 4. Backend — New Commands

| Command | Handler | Purpose |
|---|---|---|
| `UpdateDomainCommand` | `UpdateDomainHandler` | Save all editable domain fields |
| `ModifyDomainContactCommand` | `ModifyDomainContactHandler` | Send WHOIS contact update to registrar |
| `SetDnsManagementCommand` | `SetDnsManagementHandler` | Toggle DNS management on/off at registrar + entity |
| `SetEmailForwardingCommand` | `SetEmailForwardingHandler` | Toggle email forwarding on/off at registrar + entity |
| `AddEmailForwardingRuleCommand` | `AddEmailForwardingRuleHandler` | Add rule to domain + registrar |
| `UpdateEmailForwardingRuleCommand` | `UpdateEmailForwardingRuleHandler` | Update rule on domain + registrar |
| `DeleteEmailForwardingRuleCommand` | `DeleteEmailForwardingRuleHandler` | Remove rule from domain + registrar |

Each handler follows the existing pattern: load domain via `IDomainRepository`, call domain method, call registrar if applicable, save via `IUnitOfWork`.

---

## 5. Backend — New/Updated Queries

| Query | Purpose |
|---|---|
| `ListDomainsQuery` (update) | Add optional `ClientId` filter parameter for client sub-page |
| `GetDomainQuery` (update) | Include email forwarding rules and reminders in the response |

### Updated DTOs

**DomainDto** — add fields:
- `FirstPaymentAmount`, `RecurringAmount` (renamed from Price), `PaymentMethod`, `PromotionCode`, `SubscriptionId`, `AdminNotes`, `OrderId`, `OrderType`, `DnsManagement`, `EmailForwarding`
- `EmailForwardingRules: EmailForwardingRuleDto[]`
- `Reminders: DomainReminderDto[]`

**New DTOs:**
- `EmailForwardingRuleDto` — Id, Source, Destination, IsActive
- `DomainReminderDto` — Id, ReminderType, SentTo, SentAt

---

## 6. Backend — New API Endpoints

All on `DomainsController`, `[Authorize(Roles = "Admin,Reseller")]`:

| Method | Route | Purpose |
|---|---|---|
| `PUT` | `/api/domains/{id}` | Update editable domain fields |
| `POST` | `/api/domains/{id}/modify-contact` | Modify WHOIS contact at registrar |
| `PUT` | `/api/domains/{id}/dns-management` | Toggle DNS management |
| `PUT` | `/api/domains/{id}/email-forwarding-toggle` | Toggle email forwarding |
| `POST` | `/api/domains/{id}/email-forwarding` | Add email forwarding rule |
| `PUT` | `/api/domains/{id}/email-forwarding/{ruleId}` | Update email forwarding rule |
| `DELETE` | `/api/domains/{id}/email-forwarding/{ruleId}` | Delete email forwarding rule |
| `GET` | `/api/domains?clientId={id}` | Extend existing list with clientId filter |

---

## 7. Backend — EF Migration

Single migration covering:
- Rename `Price` column → `RecurringAmount` on `domains` table
- Add columns: `FirstPaymentAmount`, `PaymentMethod`, `PromotionCode`, `SubscriptionId`, `AdminNotes`, `OrderId`, `OrderType`, `DnsManagement`, `EmailForwarding`
- Create `email_forwarding_rules` table
- Create `domain_reminders` table

---

## 8. Admin Frontend — Inner Sidebar

Add "Domains" to `ClientInnerSidebar.vue`:
- Icon: globe SVG
- Label: "Domains"
- Route: `/clients/${clientId}/domains`
- Position: after "Products/Services", before "Users"

---

## 9. Admin Frontend — ClientDomainsListView.vue

Paginated table of domains for the current client. Same pattern as `ClientServicesListView.vue`.

**Columns:** ID, Domain Name, Registrar, Status, Expiry Date, Auto-Renew
**Status badges:** Active (green), Expired (red), PendingRegistration (yellow), PendingTransfer (yellow), Redemption (orange), Transferred (gray), Cancelled (gray)
**Row click:** Navigate to `/clients/:id/domains/:domainId`

---

## 10. Admin Frontend — ClientDomainDetailView.vue

Monolithic page with extracted sub-components. Layout matches WHMCS screenshot.

### 10.1 Two-Column Form (top)

**Left column:**
- Order # — read-only (`orderId`)
- Order Type — read-only ("Register" / "Transfer")
- Domain — read-only (domain name)
- Registrar — read-only
- First Payment Amount — editable number input
- Recurring Amount — editable number input
- Promotion Code — editable text input
- Subscription ID — editable text input
- Nameservers 1–5 — editable text inputs

**Right column:**
- Registration Period — editable number + "Years" label
- Registration Date — read-only date
- Expiry Date — editable date input
- Next Due Date — editable date input
- Payment Method — editable dropdown (Credit/Debit Card, Bank Transfer, PayPal)
- Status — editable dropdown (Active, Expired, PendingRegistration, PendingTransfer, Redemption, Transferred, Cancelled)

### 10.2 Registrar Commands Section

Row of action buttons:
- **Register** — calls `POST /register` (disabled if already active)
- **Renew** — opens small dialog asking for years, calls `POST /{id}/renew`
- **Modify Contact Details** — opens `DomainContactModal.vue`, calls `POST /{id}/modify-contact`
- **Get EPP Code** — calls `POST /{id}/initiate-outgoing-transfer`, shows the code in an alert/toast
- **Enable/Disable ID Protection** — calls `PUT /{id}/whois-privacy`

### 10.3 Management Tools Section

Row of toggle switches (using `ToggleSwitch.vue`):
- DNS Management — calls `PUT /{id}/dns-management`
- Email Forwarding — calls `PUT /{id}/email-forwarding-toggle`
- ID Protection (WHOIS Privacy) — calls `PUT /{id}/whois-privacy`
- Auto Renew — calls `PUT /{id}/auto-renew`
- Registrar Lock — calls `PUT /{id}/lock`

### 10.4 Email Forwarding Rules (EmailForwardingTable.vue)

Only visible when `emailForwarding` is enabled. Inline CRUD table:
- Columns: Source, Destination, Active, Actions (edit/delete)
- Add button opens inline row for new rule
- Edit toggles row to editable mode
- Delete with confirmation

### 10.5 DNS Records (DnsRecordsTable.vue)

Only visible when `dnsManagement` is enabled. Inline CRUD table:
- Columns: Type, Host, Value, TTL, Priority, Actions (edit/delete)
- Add button shows inline form row
- Type dropdown: A, AAAA, CNAME, MX, TXT, NS, SRV
- Priority field only visible for MX/SRV types

### 10.6 Domain Reminder History (DomainRemindersTable.vue)

Read-only table:
- Columns: Date, Reminder, To, Sent
- Displays all `DomainReminder` records for this domain
- Empty state: "No Records Found"

### 10.7 Admin Notes

Full-width textarea at the bottom. Saved with the main "Save Changes" action.

### 10.8 Footer

- **Save Changes** button (gradient-brand) — saves all editable fields via `PUT /api/domains/{id}`
- **Cancel Changes** button — resets form to loaded values

---

## 11. Admin Frontend — Extracted Components

| Component | Purpose |
|---|---|
| `DnsRecordsTable.vue` | DNS record CRUD table with inline editing |
| `EmailForwardingTable.vue` | Email forwarding rule CRUD table with inline editing |
| `DomainRemindersTable.vue` | Read-only reminder history table |
| `DomainContactModal.vue` | Modal form for modifying WHOIS registrant contact details |

All placed in `admin/src/modules/clients/components/`.

---

## 12. Admin Frontend — Router Changes

Add to `clients/:id` children in `router/index.ts`:

```
{ path: 'domains', component: ClientDomainsListView }
{ path: 'domains/:domainId', component: ClientDomainDetailView }
```

---

## 13. Admin Frontend — Store Changes

Extend `domainsStore.ts` with:

**State:**
- `current: DomainDetail | null` — currently loaded domain detail
- `clientDomains: DomainRegistration[]` — domains for a specific client

**Actions:**
- `fetchByClient(clientId: number)` — GET `/domains?clientId={id}`
- `fetchById(id: number)` — GET `/domains/{id}`
- `update(id: number, data: UpdateDomainPayload)` — PUT `/domains/{id}`
- `renew(id: number, years: number)` — POST `/domains/{id}/renew`
- `setAutoRenew(id: number, enabled: boolean)` — PUT `/domains/{id}/auto-renew`
- `setWhoisPrivacy(id: number, enabled: boolean)` — PUT `/domains/{id}/whois-privacy`
- `setLock(id: number, enabled: boolean)` — PUT `/domains/{id}/lock`
- `setDnsManagement(id: number, enabled: boolean)` — PUT `/domains/{id}/dns-management`
- `setEmailForwarding(id: number, enabled: boolean)` — PUT `/domains/{id}/email-forwarding-toggle`
- `modifyContact(id: number, contact: DomainContactPayload)` — POST `/domains/{id}/modify-contact`
- `getEppCode(id: number)` — POST `/domains/{id}/initiate-outgoing-transfer`
- DNS record CRUD: `addDnsRecord()`, `updateDnsRecord()`, `deleteDnsRecord()`
- Email forwarding CRUD: `addForwardingRule()`, `updateForwardingRule()`, `deleteForwardingRule()`

---

## 14. Admin Frontend — TypeScript Types

Add to `admin/src/types/models.ts`:

```typescript
interface DomainDetail {
  id: number
  clientId: number
  name: string
  tld: string
  status: string
  registeredAt: string
  expiresAt: string
  autoRenew: boolean
  whoisPrivacy: boolean
  isLocked: boolean
  registrarRef: string | null
  eppCode: string | null
  linkedServiceId: number | null
  firstPaymentAmount: number
  recurringAmount: number
  priceCurrency: string
  nextDueDate: string
  registrar: string | null
  registrationPeriod: number
  paymentMethod: string | null
  promotionCode: string | null
  subscriptionId: string | null
  adminNotes: string | null
  orderId: number | null
  orderType: string
  dnsManagement: boolean
  emailForwarding: boolean
  nameservers: NameserverDto[]
  dnsRecords: DnsRecordDto[]
  emailForwardingRules: EmailForwardingRuleDto[]
  reminders: DomainReminderDto[]
}

interface NameserverDto { id: number; host: string }
interface DnsRecordDto { id: number; type: string; host: string; value: string; ttl: number; priority: number | null }
interface EmailForwardingRuleDto { id: number; source: string; destination: string; isActive: boolean }
interface DomainReminderDto { id: number; reminderType: string; sentTo: string; sentAt: string }
```

---

## 15. File Summary

### New Files (Backend)
| File | Layer |
|---|---|
| `Domain/Domains/EmailForwardingRule.cs` | Domain |
| `Domain/Domains/DomainReminder.cs` | Domain |
| `Domain/Domains/DomainContact.cs` | Domain |
| `Application/Domains/Commands/UpdateDomain/*` | Application |
| `Application/Domains/Commands/ModifyDomainContact/*` | Application |
| `Application/Domains/Commands/SetDnsManagement/*` | Application |
| `Application/Domains/Commands/SetEmailForwarding/*` | Application |
| `Application/Domains/Commands/AddEmailForwardingRule/*` | Application |
| `Application/Domains/Commands/UpdateEmailForwardingRule/*` | Application |
| `Application/Domains/Commands/DeleteEmailForwardingRule/*` | Application |
| `Application/Domains/DTOs/EmailForwardingRuleDto.cs` | Application |
| `Application/Domains/DTOs/DomainReminderDto.cs` | Application |
| `Infrastructure/Domains/Configurations/EmailForwardingRuleConfiguration.cs` | Infrastructure |
| `Infrastructure/Domains/Configurations/DomainReminderConfiguration.cs` | Infrastructure |
| EF Migration | Infrastructure |

### Modified Files (Backend)
| File | Change |
|---|---|
| `Domain/Domains/Domain.cs` | New properties, methods, collections |
| `Domain/Domains/Interfaces/IRegistrarProvider.cs` | New methods |
| `Infrastructure/Domains/Namecheap/NamecheapRegistrarProvider.cs` | Stub implementations |
| `Infrastructure/Domains/Configurations/DomainConfiguration.cs` | New column mappings |
| `Infrastructure/Domains/DomainRepository.cs` | Eager load new collections |
| `Application/Domains/DTOs/DomainDto.cs` | New fields |
| `Application/Domains/Queries/ListDomains/*` | ClientId filter |
| `Application/Domains/Queries/GetDomain/*` | Include new collections |
| `API/Domains/DomainsController.cs` | New endpoints |

### New Files (Frontend)
| File |
|---|
| `admin/src/modules/clients/views/ClientDomainsListView.vue` |
| `admin/src/modules/clients/views/ClientDomainDetailView.vue` |
| `admin/src/modules/clients/components/DnsRecordsTable.vue` |
| `admin/src/modules/clients/components/EmailForwardingTable.vue` |
| `admin/src/modules/clients/components/DomainRemindersTable.vue` |
| `admin/src/modules/clients/components/DomainContactModal.vue` |

### Modified Files (Frontend)
| File | Change |
|---|---|
| `admin/src/modules/clients/components/ClientInnerSidebar.vue` | Add "Domains" nav item |
| `admin/src/modules/domains/stores/domainsStore.ts` | Add detail actions, client filter |
| `admin/src/router/index.ts` | Add domain routes under clients/:id |
| `admin/src/types/models.ts` | Add DomainDetail, NameserverDto, DnsRecordDto, EmailForwardingRuleDto, DomainReminderDto |
