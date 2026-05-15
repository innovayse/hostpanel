# Domains Module Design Spec

**Date:** 2026-04-17
**Status:** Approved

---

## Overview

Full domain lifecycle management module: registration, transfer (incoming + outgoing), renewal, DNS records, nameservers, WHOIS privacy, registrar lock, auto-renewal, and expiry automation. Integrates with Namecheap XML API v2 as the first registrar provider via `IRegistrarProvider`.

---

## 1. Scope

- Domain aggregate with full WHMCS-compatible status set
- `IRegistrarProvider` interface + `NamecheapRegistrarProvider` (real HTTP calls, XML API v2)
- Full DNS record management: A, AAAA, CNAME, MX, TXT, NS, SRV
- WHOIS privacy toggle + auto-renewal flag + registrar lock (transfer lock)
- Incoming transfer with EPP code + transfer cancellation
- Outgoing transfer initiation (EPP code retrieval)
- Wolverine scheduled jobs: expiry checks, renewal reminders, auto-renew
- `DomainExpiredEvent` → suspend linked `ClientService`
- `DomainRenewedEvent` → restore suspended linked `ClientService`
- Integration tests with stubbed registrar provider

---

## 2. Domain Layer

### Aggregate: `Domain`

**File:** `src/Innovayse.Domain/Domains/Domain.cs`

```csharp
public sealed class Domain : AggregateRoot
{
    private readonly List<Nameserver> _nameservers = [];
    private readonly List<DnsRecord> _dnsRecords = [];

    public int ClientId                     { get; private set; }
    public string Name                      { get; private set; }   // "example.com"
    public string Tld                       { get; private set; }   // "com"
    public DomainStatus Status              { get; private set; }
    public DateTimeOffset RegisteredAt      { get; private set; }
    public DateTimeOffset ExpiresAt         { get; private set; }
    public bool AutoRenew                   { get; private set; }
    public bool WhoisPrivacy                { get; private set; }
    public bool IsLocked                    { get; private set; }
    public string? RegistrarRef             { get; private set; }
    public string? EppCode                  { get; private set; }
    public int? LinkedServiceId             { get; private set; }

    public IReadOnlyList<Nameserver> Nameservers => _nameservers.AsReadOnly();
    public IReadOnlyList<DnsRecord> DnsRecords   => _dnsRecords.AsReadOnly();
}
```

**Factory methods:**
- `Domain.Register(int clientId, string name, DateTimeOffset expiresAt, bool autoRenew, bool whoisPrivacy)` → status `PendingRegistration`
- `Domain.CreateTransfer(int clientId, string name)` → status `PendingTransfer`

**Business methods:**
- `Activate(string registrarRef)` → `PendingRegistration → Active`, raises `DomainRegisteredEvent`
- `ActivateTransfer(string registrarRef, DateTimeOffset expiresAt)` → `PendingTransfer → Active`, raises `DomainTransferredInEvent`
- `MarkExpired()` → `Active → Expired`, raises `DomainExpiredEvent`
- `MarkRedemption()` → `Expired → Redemption`
- `MarkTransferred()` → `Active → Transferred`
- `Cancel()` → `PendingRegistration | PendingTransfer | Expired | Redemption → Cancelled`
- `Renew(DateTimeOffset newExpiresAt)` → extends expiry, raises `DomainRenewedEvent`
- `SetAutoRenew(bool value)`
- `SetWhoisPrivacy(bool value)`
- `SetLock(bool value)`
- `SetEppCode(string code)`
- `SetNameservers(IReadOnlyList<string> hosts)` — clears and replaces `_nameservers`
- `AddDnsRecord(DnsRecordType type, string host, string value, int ttl, int? priority)`
- `RemoveDnsRecord(int recordId)`
- `UpdateDnsRecord(int recordId, string value, int ttl, int? priority)`
- `LinkService(int serviceId)`

**Guards:**
- `Activate` throws if status is not `PendingRegistration`
- `MarkExpired` throws if status is not `Active`
- `MarkRedemption` throws if status is not `Expired`
- `Renew` throws if status is `Cancelled` or `Transferred`
- `Cancel` throws if status is `Active`, `Transferred`, or `Cancelled`

---

### Entity: `Nameserver`

**File:** `src/Innovayse.Domain/Domains/Nameserver.cs`

Properties: `Id`, `DomainId`, `Host` (e.g. `ns1.namecheap.com`)

---

### Entity: `DnsRecord`

**File:** `src/Innovayse.Domain/Domains/DnsRecord.cs`

Properties: `Id`, `DomainId`, `Type` (`DnsRecordType`), `Host`, `Value`, `Ttl`, `Priority?` (nullable int, for MX/SRV)

---

### Enums

**File:** `src/Innovayse.Domain/Domains/DomainStatus.cs`
```csharp
public enum DomainStatus
{
    PendingRegistration,
    PendingTransfer,
    Active,
    Expired,
    Redemption,
    Transferred,
    Cancelled
}
```

**File:** `src/Innovayse.Domain/Domains/DnsRecordType.cs`
```csharp
public enum DnsRecordType { A, AAAA, CNAME, MX, TXT, NS, SRV }
```

---

### Domain Events

**Files:** `src/Innovayse.Domain/Domains/Events/`

```csharp
record DomainRegisteredEvent(int DomainId, int ClientId, string Name) : IDomainEvent;
record DomainTransferredInEvent(int DomainId, int ClientId, string Name) : IDomainEvent;
record DomainRenewedEvent(int DomainId, int ClientId, DateTimeOffset NewExpiresAt) : IDomainEvent;
record DomainExpiredEvent(int DomainId, int ClientId, int? LinkedServiceId) : IDomainEvent;
record DomainExpiringEvent(int DomainId, int ClientId, string Name, DateTimeOffset ExpiresAt) : IDomainEvent;
```

---

### Repository Interface

**File:** `src/Innovayse.Domain/Domains/Interfaces/IDomainRepository.cs`

```csharp
public interface IDomainRepository
{
    Task<Domain?> FindByIdAsync(int id, CancellationToken ct);
    Task<Domain?> FindByNameAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<Domain>> ListByClientAsync(int clientId, CancellationToken ct);
    Task<IReadOnlyList<Domain>> ListExpiringBeforeAsync(DateTimeOffset threshold, CancellationToken ct);
    Task<IReadOnlyList<Domain>> ListAutoRenewDueAsync(DateTimeOffset threshold, CancellationToken ct);
    Task<(IReadOnlyList<Domain> Items, int TotalCount)> PagedListAsync(int page, int pageSize, CancellationToken ct);
    void Add(Domain domain);
}
```

---

### Registrar Interface

**File:** `src/Innovayse.Domain/Domains/Interfaces/IRegistrarProvider.cs`

```csharp
public interface IRegistrarProvider
{
    Task<RegistrarResult> RegisterAsync(RegisterDomainRequest req, CancellationToken ct);
    Task<RegistrarResult> TransferAsync(TransferDomainRequest req, CancellationToken ct);
    Task<RegistrarResult> RenewAsync(RenewDomainRequest req, CancellationToken ct);
    Task CancelTransferAsync(string registrarRef, CancellationToken ct);
    Task InitiateOutgoingTransferAsync(string domainName, CancellationToken ct);
    Task SetAutoRenewAsync(string registrarRef, bool value, CancellationToken ct);
    Task SetWhoisPrivacyAsync(string registrarRef, bool value, CancellationToken ct);
    Task SetRegistrarLockAsync(string registrarRef, bool value, CancellationToken ct);
    Task<string> GetEppCodeAsync(string registrarRef, CancellationToken ct);
    Task SetNameserversAsync(string registrarRef, IReadOnlyList<string> nameservers, CancellationToken ct);
    Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(string domainName, CancellationToken ct);
    Task AddDnsRecordAsync(string domainName, DnsRecord record, CancellationToken ct);
    Task UpdateDnsRecordAsync(string domainName, DnsRecord record, CancellationToken ct);
    Task DeleteDnsRecordAsync(string domainName, int recordId, CancellationToken ct);
    Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct);
    Task<WhoisInfo> GetWhoisAsync(string domainName, CancellationToken ct);
}

public record RegistrarResult(bool Success, string? RegistrarRef, DateTimeOffset? ExpiresAt, string? ErrorMessage);
public record RegisterDomainRequest(string DomainName, int Years, bool WhoisPrivacy, bool AutoRenew, string? Nameserver1, string? Nameserver2);
public record TransferDomainRequest(string DomainName, string EppCode, bool WhoisPrivacy);
public record RenewDomainRequest(string DomainName, string RegistrarRef, int Years);
public record WhoisInfo(string Registrar, string Registrant, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
```

---

## 3. Application Layer

### Commands

**Directory:** `src/Innovayse.Application/Domains/Commands/`

| Command | Handler returns |
|---------|----------------|
| `RegisterDomainCommand(int ClientId, string DomainName, int Years, bool WhoisPrivacy, bool AutoRenew, string? Ns1, string? Ns2)` | `int` (domain id) |
| `TransferDomainCommand(int ClientId, string DomainName, string EppCode, bool WhoisPrivacy)` | `int` |
| `RenewDomainCommand(int DomainId, int Years)` | `void` |
| `CancelTransferCommand(int DomainId)` | `void` |
| `InitiateOutgoingTransferCommand(int DomainId)` | `string` (EPP code) |
| `SetAutoRenewCommand(int DomainId, bool Value)` | `void` |
| `SetWhoisPrivacyCommand(int DomainId, bool Value)` | `void` |
| `SetRegistrarLockCommand(int DomainId, bool Value)` | `void` |
| `UpdateNameserversCommand(int DomainId, IReadOnlyList<string> Nameservers)` | `void` |
| `AddDnsRecordCommand(int DomainId, DnsRecordType Type, string Host, string Value, int Ttl, int? Priority)` | `void` |
| `UpdateDnsRecordCommand(int DomainId, int RecordId, string Value, int Ttl, int? Priority)` | `void` |
| `DeleteDnsRecordCommand(int DomainId, int RecordId)` | `void` |
| `MarkDomainExpiredCommand(int DomainId)` | `void` |
| `MarkDomainRedemptionCommand(int DomainId)` | `void` |
| `CheckDomainExpiriesCommand` | `void` (scheduled) |
| `AutoRenewDomainsCommand` | `void` (scheduled) |

All handlers load domain via `IDomainRepository.FindByIdAsync`, call registrar where needed, call domain method, save via `IUnitOfWork.SaveChangesAsync`. Throw `InvalidOperationException` when domain not found.

---

### Queries

**Directory:** `src/Innovayse.Application/Domains/Queries/`

| Query | Returns |
|-------|---------|
| `GetDomainQuery(int DomainId)` | `DomainDto` |
| `ListDomainsQuery(int Page, int PageSize)` | `PagedResult<DomainListItemDto>` |
| `GetMyDomainsQuery(int ClientId)` | `IReadOnlyList<DomainDto>` |
| `CheckDomainAvailabilityQuery(string DomainName)` | `bool` |
| `GetWhoisQuery(string DomainName)` | `WhoisDto` |

---

### DTOs

**Directory:** `src/Innovayse.Application/Domains/DTOs/`

```csharp
record DomainDto(int Id, int ClientId, string Name, string Tld,
    DomainStatus Status, DateTimeOffset RegisteredAt, DateTimeOffset ExpiresAt,
    bool AutoRenew, bool WhoisPrivacy, bool IsLocked,
    string? RegistrarRef, string? EppCode, int? LinkedServiceId,
    IReadOnlyList<NameserverDto> Nameservers,
    IReadOnlyList<DnsRecordDto> DnsRecords);

record DomainListItemDto(int Id, int ClientId, string Name, DomainStatus Status,
    DateTimeOffset ExpiresAt, bool AutoRenew);

record NameserverDto(int Id, string Host);
record DnsRecordDto(int Id, DnsRecordType Type, string Host, string Value, int Ttl, int? Priority);
record WhoisDto(string Registrar, string Registrant, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
```

---

### Wolverine Event Handlers

**Directory:** `src/Innovayse.Application/Domains/Events/`

- `DomainExpiredHandler` — handles `DomainExpiredEvent` → if `LinkedServiceId` is set, dispatches `SuspendServiceCommand`
- `DomainRenewedHandler` — handles `DomainRenewedEvent` → if linked service was suspended, dispatches reactivation

---

## 4. Infrastructure Layer

### `NamecheapClient`

**File:** `src/Innovayse.Infrastructure/Domains/Namecheap/NamecheapClient.cs`

Internal typed `HttpClient`. All Namecheap API calls use HTTP GET with query string parameters:
- `ApiUser`, `ApiKey`, `UserName`, `ClientIp`, `Command` — always included
- Domain-specific params appended per command
- Response parsed via `System.Xml.Linq` (`XDocument.Parse`)
- `Status="OK"` → success; `Status="ERROR"` → parse `<Error>` node, throw `RegistrarException`

**Namecheap error code → exception mapping:**
| Code | Exception |
|------|-----------|
| `2019166` | `InsufficientFundsException` |
| `2030280` | `DomainNotAvailableException` |
| `2011170` | `InvalidEppCodeException` |
| all others | `RegistrarException(code, message)` |

---

### `NamecheapRegistrarProvider`

**File:** `src/Innovayse.Infrastructure/Domains/Namecheap/NamecheapRegistrarProvider.cs`

Implements `IRegistrarProvider`. Delegates all HTTP work to `NamecheapClient`.

DNS add/update/delete uses `namecheap.domains.dns.setHosts` (Namecheap replaces the entire host list per call):
1. Fetch current records via `GetDnsRecordsAsync`
2. Apply the mutation (add/update/delete) to the in-memory list
3. Call `setHosts` with the full updated list

---

### `NamecheapSettings`

**File:** `src/Innovayse.Infrastructure/Domains/Namecheap/NamecheapSettings.cs`

```csharp
public sealed class NamecheapSettings
{
    public required string ApiUser  { get; init; }
    public required string ApiKey   { get; init; }
    public required string ClientIp { get; init; }
    public required string ApiUrl   { get; init; }
    public bool Sandbox             { get; init; }
}
```

Bound via `IOptions<NamecheapSettings>` from `"Namecheap"` config section.

---

### EF Core Configurations

**Directory:** `src/Innovayse.Infrastructure/Domains/Configurations/`

- `DomainConfiguration` — table `domains`, `Status` as string (max 30), `Name` unique index
- `NameserverConfiguration` — table `nameservers`, FK `DomainId` cascade
- `DnsRecordConfiguration` — table `dns_records`, `Type` as string (max 10), `Priority` nullable, FK `DomainId` cascade

**`DomainRepository`** — `FindByIdAsync` includes `Nameservers` + `DnsRecords` via lambda includes + `UsePropertyAccessMode(PropertyAccessMode.Field)`

---

### DependencyInjection.cs

```csharp
services.AddScoped<IDomainRepository, DomainRepository>();
services.AddScoped<IRegistrarProvider, NamecheapRegistrarProvider>();
services.AddHttpClient<NamecheapClient>(client =>
{
    client.BaseAddress = new Uri(configuration["Namecheap:ApiUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});
services.Configure<NamecheapSettings>(configuration.GetSection("Namecheap"));
```

Wolverine scheduled jobs (in `Program.cs` `UseWolverine` block):
```csharp
opts.SchedulePublish(new CheckDomainExpiriesCommand()).Daily().At(9, 0);
opts.SchedulePublish(new AutoRenewDomainsCommand()).Daily().At(10, 0);
```

---

## 5. API Layer

### `DomainsController`

**Route:** `api/domains` | **Auth:** `Admin, Reseller`

| Method | Route | Body/Query | Response |
|--------|-------|-----------|----------|
| GET | `/` | `?page=1&pageSize=20` | 200 `PagedResult<DomainListItemDto>` |
| GET | `/{id}` | — | 200 `DomainDto` |
| GET | `/check` | `?name=example.com` | 200 `bool` |
| GET | `/whois` | `?name=example.com` | 200 `WhoisDto` |
| POST | `/register` | `RegisterDomainRequest` | 201 `int` |
| POST | `/transfer` | `TransferDomainRequest` | 201 `int` |
| POST | `/{id}/renew` | `RenewRequest { int Years }` | 204 |
| POST | `/{id}/cancel-transfer` | — | 204 |
| POST | `/{id}/initiate-transfer-out` | — | 200 `string` (EPP code) |
| POST | `/{id}/auto-renew` | `{ bool Value }` | 204 |
| POST | `/{id}/whois-privacy` | `{ bool Value }` | 204 |
| POST | `/{id}/lock` | `{ bool Value }` | 204 |
| POST | `/{id}/nameservers` | `{ string[] Nameservers }` | 204 |
| POST | `/{id}/dns` | `AddDnsRecordRequest` | 204 |
| PUT | `/{id}/dns/{recordId}` | `UpdateDnsRecordRequest` | 204 |
| DELETE | `/{id}/dns/{recordId}` | — | 204 |

### `MyDomainsController`

**Route:** `api/me/domains` | **Auth:** `Client`

| Method | Route | Response |
|--------|-------|----------|
| GET | `/` | 200 `IReadOnlyList<DomainDto>` |
| GET | `/{id}` | 200 `DomainDto` or 403 |
| POST | `/{id}/auto-renew` | ownership check → 204 |
| POST | `/{id}/whois-privacy` | ownership check → 204 |

### Request DTOs

**Directory:** `src/Innovayse.API/Domains/Requests/`

```csharp
sealed class RegisterDomainRequest
{
    public required int ClientId      { get; init; }
    public required string DomainName { get; init; }
    public required int Years         { get; init; }
    public bool WhoisPrivacy          { get; init; }
    public bool AutoRenew             { get; init; }
    public string? Nameserver1        { get; init; }
    public string? Nameserver2        { get; init; }
}

sealed class TransferDomainRequest
{
    public required int ClientId      { get; init; }
    public required string DomainName { get; init; }
    public required string EppCode    { get; init; }
    public bool WhoisPrivacy          { get; init; }
}

sealed class RenewRequest        { public required int Years         { get; init; } }
sealed class SetValueRequest     { public required bool Value        { get; init; } }
sealed class UpdateNameserversRequest { public required IReadOnlyList<string> Nameservers { get; init; } }

sealed class AddDnsRecordRequest
{
    public required DnsRecordType Type { get; init; }
    public required string Host        { get; init; }
    public required string Value       { get; init; }
    public required int Ttl            { get; init; }
    public int? Priority               { get; init; }
}

sealed class UpdateDnsRecordRequest
{
    public required string Value { get; init; }
    public required int Ttl      { get; init; }
    public int? Priority         { get; init; }
}
```

---

## 6. Integration Tests

**File:** `tests/Innovayse.Integration.Tests/Domains/DomainEndpointTests.cs`

`IntegrationTestFactory` overrides `IRegistrarProvider` with `StubRegistrarProvider` (always returns success, generates fake `RegistrarRef` and `ExpiresAt = UtcNow.AddYears(1)`).

Tests:
1. `GET /api/domains` without auth → 401
2. `GET /api/me/domains` without auth → 401
3. `GET /api/me/domains` as admin → 403
4. `GET /api/domains` as admin → 200
5. `GET /api/domains/check?name=test.com` as admin → 200 bool
6. Admin registers domain → 201, then `GET /api/domains/{id}` → 200 with `Active` status
7. Client `GET /api/me/domains` → 200 empty list
8. Client cannot access another client's domain → 403

---

## 7. Implementation Order

1. Domain aggregate + entities + enums + events + tests
2. `IRegistrarProvider` + `IDomainRepository` interfaces + request/result records
3. Application — DTOs + all commands + validators
4. Application — all queries + event handlers
5. Application — `CheckDomainExpiriesCommand` + `AutoRenewDomainsCommand` scheduled handlers
6. Infrastructure — `NamecheapSettings` + `NamecheapClient` (HTTP + XML parsing)
7. Infrastructure — `NamecheapRegistrarProvider` (all API operations)
8. Infrastructure — EF configs + `DomainRepository` + `AppDbContext` update + `DI` update
9. Infrastructure — EF Migration `AddDomains`
10. API — `DomainsController` + `MyDomainsController` + request DTOs
11. Integration Tests — `StubRegistrarProvider` in factory + `DomainEndpointTests`
12. `dotnet format --verify-no-changes`
