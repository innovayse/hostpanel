# Clean Architecture Rules for the Hostpanel Backend

## Layer Dependencies (STRICT)

```
API → Application → Domain
Infrastructure → Application (implements the ports in Application/Common/Interfaces)
Application ← Infrastructure (via DI, never direct instantiation)
```

- **Domain** has zero NuGet dependencies (no EF Core, no ASP.NET, nothing)
- **Application** depends only on Domain, never on Infrastructure or API
- **Infrastructure** implements the Application ports (`Application/Common/Interfaces/`) and never calls a handler
- **API** is thin: controllers bind the command or query and dispatch it through Wolverine's `IMessageBus`

## Domain Layer Rules

- Every entity extends `BaseEntity` (or `AuditableEntity`) from the SharedKernel; aggregates also implement `IAggregateRoot`
- All value objects extend `ValueObject`
- Domain events implement `IDomainEvent` and are raised inside the entity via `AddDomainEvent()`
- No public setters on entities: use methods that encode business rules
- No static methods that mutate state; `static Create` factories only build a new instance
- No `async` in domain: domain is pure logic, no I/O, and no clock (the moment is passed in)

```csharp
// CORRECT
public sealed class Invoice : BaseEntity, IAggregateRoot
{
    /// <summary>Internal mutable list of line items.</summary>
    private readonly List<InvoiceItem> _items = [];

    /// <summary>Creates an unpaid invoice.</summary>
    public static Invoice Create(int clientId, DateTimeOffset dueDate, string currency, DateTimeOffset issuedAt) { ... }

    /// <summary>Adds a line item while the invoice is a draft or unpaid.</summary>
    public void AddItem(string description, decimal unitPrice, int quantity, int? clientServiceId = null) { ... }
}

// WRONG: fix it wherever you see it
public decimal Total { get; set; } // public setter
```

## Application Layer Rules

- One folder per use case: `Commands/CreateInvoice/` holds `CreateInvoiceCommand.cs`, `CreateInvoiceHandler.cs`
  and `CreateInvoiceValidator.cs`, and nothing else
- Commands return a `Result` or `Result<T>` with a primitive or a DTO, never a domain entity
- Queries return DTOs, never domain entities
- No business logic in handlers: delegate to domain entities or domain services
- No repositories and no unit-of-work port: handlers read and write through `IHostpanelDbContext`
  with LINQ, change state through entity methods, and save with `SaveChangesAsync` (the unit of work)
- The current moment comes from `IClock`, never `DateTimeOffset.UtcNow`

```csharp
// Command
public sealed record CreateInvoiceCommand(
    int ClientId,
    DateTimeOffset DueDate,
    IReadOnlyList<CreateInvoiceItemDto> Items,
    bool IsDraft = false);

// Handler
public sealed class CreateInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the currency the invoiced client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The clock the issue moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="payerCurrency">Resolves the currency the invoiced client is billed in.</param>
    /// <param name="clock">The clock the issue moment is read from.</param>
    public CreateInvoiceHandler(IHostpanelDbContext context, IPayerCurrencyResolver payerCurrency, IClock clock)
    {
        _context = context;
        _payerCurrency = payerCurrency;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The create invoice command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the new invoice id.</returns>
    public async Task<Result<int>> HandleAsync(CreateInvoiceCommand cmd, CancellationToken ct)
    {
        var currency = (await _payerCurrency.ForClientAsync(cmd.ClientId, ct)).Code;
        var invoice = Invoice.Create(cmd.ClientId, cmd.DueDate, currency, cmd.IsDraft, _clock.UtcNow);
        foreach (var item in cmd.Items)
        {
            invoice.AddItem(item.Description, item.UnitPrice, item.Quantity);
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(invoice.Id);
    }
}
```

## Infrastructure Layer Rules

- EF Core `DbContext`, migrations, and configurations live here only
- `AppDbContext` (in `Infrastructure/Persistence/`) implements `IHostpanelDbContext`; there are no repository classes
- Adapters implement the Application ports, under a capability folder
- External service clients (payment gateways, registrars, control panels) live in `Infrastructure/Integrations/`
- No business logic: only I/O, mapping, persistence

## API Layer Rules

- Controllers derive from `BaseApiController`, take `IMessageBus` in an explicit constructor and
  keep it in a documented `private readonly` field
- One dispatch per action: `_bus.InvokeAsync<Result<T>>(...)`, answered with `HandleResult`
  (`CreatedResult` for a 201)
- Auth: `[Authorize(Policy = Policies.X)]` on every action, never inline claims checks
- `[ProducesResponseType]` for every status the action can answer, `typeof(ErrorResponse)` on every non-2xx
- No DbContext injection in controllers

```csharp
[Route("api/billing")]
public sealed class InvoicesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public InvoicesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Creates an invoice.</summary>
    /// <param name="command">The invoice to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new id; 400 when refused; 401; 403 for a caller who is not an admin.</returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateInvoiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/billing/{id}", id));
    }
}
```

## Wolverine Rules

- Commands and queries: `_bus.InvokeAsync<Result<T>>()`, synchronous request/response
- Events: `PublishAsync()`, handled asynchronously
- Handlers are plain classes: no interface needed, convention-based
- All event handlers that do I/O must be idempotent (safe to retry)
- Domain events are collected on `BaseEntity.DomainEvents` and written by
  `Infrastructure/Persistence/DomainEventDispatchInterceptor` to Wolverine's PostgreSQL outbox
  (`IDbContextOutbox`) in the same transaction as the change, then cleared: a rollback drops them and a
  publish failure fails the save. `IDomainEventGate` suppresses dispatch for bulk writers (see
  "Domain events are dispatched" and "Durable messaging" in `project.md`)

## Pluggable Interface Rules

- One-off card payments at checkout go through `ICardPaymentGateway`; hosted bank pages go through the plugin SDK
  contract `IPaymentPlugin` (`Innovayse.SDK.Plugins`), which Application reaches through
  `IPaymentPluginResolver`; saved payment methods and off-session charges go through the optional SDK capability
  `IStoredCredentialPlugin`, which Application reaches only through `StoredCredentialGateway` (built on
  `IStoredCredentialPluginResolver`), so the built-in Stripe gateway and a payment plugin are charged the same way;
  there is no other direct-charge gateway
- Registrar providers implement `IRegistrarProvider`
- Provisioning providers implement `IProvisioningProvider`
- Ports live in **Application/Common/Interfaces**, implementations in **Infrastructure**
- Never `new` a gateway directly: always inject the interface

## Folder Structure per Module

```
Application/
  Billing/
    Commands/
      CreateInvoice/
        CreateInvoiceCommand.cs
        CreateInvoiceHandler.cs
        CreateInvoiceValidator.cs
    Queries/
      GetInvoice/
        GetInvoiceQuery.cs
        GetInvoiceHandler.cs
        GetInvoiceValidator.cs
    Common/
      InvoiceDto.cs
      CreateInvoiceItemDto.cs
      Enums/
        RefundType.cs
    Constants/
      CurrencyCodes.cs
    Events/
      RenewalPaymentReceivedHandler.cs
    Extensions/
      InvoiceMappingExtensions.cs
```

Every DTO and every helper type a use case needs lives in `<Module>/Common/` (enums it exposes in
`Common/Enums/`), never in the use case folder.

## Extension Classes

- One `public static class` per file, named `<WhatItExtends>Extensions`: file name matches the class
- Lives in an `Extensions/` folder **inside the layer or feature that owns it**, never one
  project-wide `Extensions/` bucket at the root
- Never appended to the DTO, record or controller it serves. A `ToDto()` helper at the bottom of a
  file that already holds five records is a grab-bag file, and it hides the mapping from anyone who
  did not already know which file to open
- Namespace matches the folder path, as everywhere else
- It goes in the layer where it is **used**, which is usually the layer that owns the type it
  extends:
  - Mapping a Domain entity to an Application DTO: **Application**. The DTO is the
    Application's, and Domain must not learn the shape it is projected into
  - Converting an enum to an HTTP wire string: **API**. The wire format is the API's business,
    and the enum never leaves the backend in that form
- Full XML docs: class summary, method summary, every `<param>`, `<returns>`, and `<exception>` the
  body can throw, private `const` members included, because a cap like `999` is a decision and
  the summary is the only place its reason survives
- Exception: DI registration methods (`AddApplication(this IServiceCollection services)`) are
  composition-root wiring, not extensions of a type. They stay in `DependencyInjection.cs`

```csharp
// src/Innovayse.Hostpanel.Application/Migration/Extensions/MigrationJobDtoExtensions.cs
namespace Innovayse.Hostpanel.Application.Migration.Extensions;

/// <summary>Extension methods for mapping <see cref="MigrationJob"/> to DTOs.</summary>
public static class MigrationJobDtoExtensions
{
    /// <summary>Maps a <see cref="MigrationJob"/> aggregate to a <see cref="MigrationJobDto"/>.</summary>
    /// <param name="j">The job to map.</param>
    /// <param name="now">The current moment, against which the last ping is judged live or stale.</param>
    /// <returns>The DTO describing the job and its per-entity progress.</returns>
    public static MigrationJobDto ToDto(this MigrationJob j, DateTimeOffset now) { ... }
}
```

## Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Command | `VerbNounCommand` | `CreateInvoiceCommand` |
| Query | `GetNounQuery` | `GetInvoiceQuery` |
| Handler | `VerbNounHandler` | `CreateInvoiceHandler` |
| Event | `NounVerbedEvent` | `InvoiceCreatedEvent` |
| DTO | `NounDto` in `<Module>/Common/` | `InvoiceDto` |
| Port | `INoun` in `Application/Common/Interfaces/` | `ICardPaymentGateway` |
| Adapter | implementation under a capability folder in Infrastructure | `Integrations/Stripe/StripeService` |
| Extension class | `NounExtensions` | `MigrationJobDtoExtensions` |

- Every database identifier we own is PascalCase: tables (plural), columns, primary/foreign keys, indexes (`IX_<Table>_<Cols>`), unique/check constraints (`CK_<Table>_<Rule>`), schemas we create. No snake_case or lower case; see "Database identifiers" in `project.md`.
