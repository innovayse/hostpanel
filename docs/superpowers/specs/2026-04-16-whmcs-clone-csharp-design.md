# Innovayse Backend — WHMCS Clone (C# Monolith) Design Spec
**Date:** 2026-04-16  
**Status:** Approved

---

## Overview

A full WHMCS replacement built as a C# Clean Architecture monolith with PostgreSQL. The existing Nuxt.js client and a new Vue.js admin panel both connect to it via server-side proxy — the C# API URL is never exposed to the browser.

---

## 1. Solution Structure

```
Innovayse.Backend/
├── src/
│   ├── Innovayse.Domain/          # Entities, Value Objects, Domain Events, Interfaces
│   ├── Innovayse.Application/     # Use Cases, Commands/Queries (CQRS), DTOs, Validators
│   ├── Innovayse.Infrastructure/  # EF Core, PostgreSQL, Payment/Registrar/Provisioning impls
│   └── Innovayse.API/             # ASP.NET Core, Controllers, Middleware, Auth
├── tests/
│   ├── Innovayse.Domain.Tests/
│   ├── Innovayse.Application.Tests/
│   └── Innovayse.Integration.Tests/
└── Innovayse.Backend.sln
```

### Dependency Rule
```
API → Application → Domain
Infrastructure → Domain (implements interfaces)
Infrastructure ← Application (injected via DI)
```

---

## 2. Domain Layer

```
Domain/
├── Clients/        Client, Contact, Address
├── Billing/        Invoice, InvoiceItem, Payment, Subscription
├── Products/       Product, ProductGroup, Pricing
├── Services/       ClientService, ServiceStatus
├── Domains/        Domain, Nameserver, DomainStatus
├── Support/        Ticket, TicketReply, Department
├── Notifications/  EmailTemplate, EmailLog
└── Common/         AggregateRoot, Entity, ValueObject, IDomainEvent
```

All domain objects are free of framework dependencies. Domain events are raised inside aggregates and dispatched by Wolverine after persistence.

---

## 3. Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 + Npgsql |
| CQRS + Messaging | Wolverine |
| Validation | FluentValidation |
| Auth | ASP.NET Core Identity + JWT + Refresh Tokens |
| Mapping | Mapster |
| Email | MailKit |
| Payment | Stripe SDK + `IPaymentGateway` interface |
| Registrar | `IRegistrarProvider` → Namecheap / ResellerClub / ENOM |
| Provisioning | `IProvisioningProvider` → cPanel WHM API (Plesk later) |
| Logging | Serilog → file + PostgreSQL |
| API Docs | Scalar |

---

## 4. CQRS with Wolverine

```csharp
// Command — convention-based, no interface required
public record CreateInvoiceCommand(int ClientId, List<InvoiceItemDto> Items);

public class CreateInvoiceHandler
{
    public async Task<int> Handle(CreateInvoiceCommand cmd, IInvoiceRepository repo) { ... }
}

// Dispatch
await bus.InvokeAsync(new CreateInvoiceCommand(...));
await bus.PublishAsync(new InvoiceCreatedEvent(...));
```

Wolverine replaces both MediatR and Hangfire — it handles in-process messaging, scheduled jobs, durable outbox, and retry policies.

---

## 5. Pluggable Interfaces

```csharp
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct);
}

public interface IRegistrarProvider
{
    Task<DomainResult> RegisterAsync(RegisterDomainRequest request, CancellationToken ct);
    Task<DomainResult> TransferAsync(TransferDomainRequest request, CancellationToken ct);
    Task RenewAsync(RenewDomainRequest request, CancellationToken ct);
}

public interface IProvisioningProvider
{
    Task ProvisionAsync(ProvisionRequest request, CancellationToken ct);
    Task SuspendAsync(string serviceId, CancellationToken ct);
    Task TerminateAsync(string serviceId, CancellationToken ct);
}
```

Implementation is selected from configuration via DI registration. Adding a new gateway = implement interface + register.

---

## 6. Database Schema (EF Core, code-first)

```
clients, contacts, addresses
products, product_groups, product_pricing
client_services, service_addons
invoices, invoice_items, payments, subscriptions
domains, nameservers, domain_contacts
tickets, ticket_replies, departments
email_templates, email_logs
refresh_tokens, audit_logs
```

---

## 7. Authentication

```
POST /api/auth/login    → AccessToken (15min, response body) + RefreshToken (7d, httpOnly cookie)
POST /api/auth/refresh  → new AccessToken
POST /api/auth/logout   → revoke RefreshToken
```

Roles: `Admin`, `Reseller`, `Client`. ASP.NET Core Identity manages users and roles.

The frontend servers (Nuxt, Vue Admin) store the JWT in memory or httpOnly cookies — never in localStorage.

---

## 8. API Routes

```
/api/auth/          login, register, refresh, logout
/api/clients/       CRUD, contacts, addresses
/api/billing/       invoices, payments, subscriptions
/api/services/      client services, addons, provisioning actions
/api/domains/       register, transfer, renew, DNS, WHOIS
/api/support/       tickets, replies, departments, KB articles
/api/admin/         dashboard stats, settings, email templates, reports
```

---

## 9. Notifications + Background Jobs

### Domain Events → Wolverine Handlers

| Event | Handler(s) |
|-------|-----------|
| `ClientRegisteredEvent` | SendWelcomeEmailHandler |
| `InvoiceCreatedEvent` | SendInvoiceEmailHandler |
| `InvoiceOverdueEvent` | SendOverdueReminderHandler, SuspendServiceHandler |
| `PaymentReceivedEvent` | ActivateServiceHandler, SendReceiptHandler |
| `ServiceProvisionedEvent` | SendCredentialsEmailHandler |
| `DomainExpiringEvent` | SendRenewalReminderHandler |
| `TicketCreatedEvent` | NotifyDepartmentHandler |

### Scheduled Jobs (Wolverine cron)

```csharp
Schedule(() => bus.PublishAsync(new GenerateRecurringInvoicesCommand()),  "0 8 * * *");
Schedule(() => bus.PublishAsync(new CheckDomainExpiriesCommand()),        "0 9 * * *");
Schedule(() => bus.PublishAsync(new SuspendOverdueServicesCommand()),     "0 10 * * *");
```

### Wolverine Outbox

All domain events are persisted in the outbox within the same DB transaction as the aggregate. Wolverine delivers them with guaranteed delivery + retry.

### Email Templates

Stored in `email_templates` table. Templates use Liquid/Handlebars syntax with variables like `{{client.name}}`, `{{invoice.total}}`. Editable from Vue admin panel.

---

## 10. Frontend Integration

### Architecture (Proxy Pattern)

```
Browser → Nuxt Server (proxy) → C# API (internal network)
Browser → Vue Admin Server (proxy) → C# API (internal network)
```

The C# API URL is never exposed to the browser. All requests go through the respective server-side proxy.

### Nuxt Client (existing project)

`server/utils/whmcs.ts` is replaced by `server/utils/api.ts`. All existing Nuxt server routes (`/server/api/portal/**`) remain — only the internal call changes from `whmcsCall` to `internalApiCall`.

```ts
// server/utils/api.ts
export async function internalApiCall<T>(endpoint: string, params = {}) {
  return $fetch<T>(`${config.apiUrl}${endpoint}`, {
    headers: { Authorization: `Bearer ${serverSideToken}` },
    body: params
  })
}
```

### Vue Admin Panel (new project)

```
innovayse-admin/
├── src/
│   ├── modules/
│   │   ├── clients/
│   │   ├── billing/
│   │   ├── services/
│   │   ├── domains/
│   │   ├── support/
│   │   └── settings/     # products, email templates, gateways
│   ├── composables/      # useApi, useAuth
│   ├── stores/           # Pinia
│   └── router/           # Vue Router
```

**Stack:** Vite + Vue 3 + TypeScript + Pinia + Vue Router + Tailwind CSS + shadcn-vue

**Dev proxy (vite.config.ts):**
```ts
proxy: {
  '/proxy': {
    target: 'http://localhost:5000',
    rewrite: path => path.replace(/^\/proxy/, '/api')
  }
}
```

**Prod proxy (Nginx):**
```nginx
location /proxy/ {
  proxy_pass http://localhost:5000/api/;
}
```

### Auth Cookie Flow

```
Login → C# API returns JWT
      → Nuxt/Admin server sets httpOnly cookie
      → Browser never sees the token
      → Subsequent requests auto-attach cookie
```

---

## 11. Implementation Order (Sub-projects)

1. **Core Setup** — Solution structure, Domain common, EF Core, PostgreSQL, Wolverine wiring
2. **Auth** — Identity, JWT, Refresh tokens, roles
3. **Clients** — CRUD, contacts, addresses
4. **Products & Services** — Products, pricing, client services
5. **Billing** — Invoices, payments, subscriptions, Stripe gateway
6. **Domains** — Registrar integration (Namecheap first)
7. **Provisioning** — cPanel WHM API
8. **Support** — Tickets, departments, KB
9. **Notifications** — Email templates, Wolverine outbox handlers
10. **Admin API** — Dashboard, reports, settings endpoints
11. **Nuxt migration** — Replace whmcs.ts with api.ts
12. **Vue Admin** — Admin panel frontend
