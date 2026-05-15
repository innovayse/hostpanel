# Clean Architecture Rules — Innovayse Backend

## Layer Dependencies (STRICT)

```
API → Application → Domain
Infrastructure → Domain (implements interfaces only)
Infrastructure ← Application (via DI, never direct instantiation)
```

- **Domain** has zero NuGet dependencies (no EF Core, no ASP.NET, nothing)
- **Application** depends only on Domain — never on Infrastructure or API
- **Infrastructure** implements Domain interfaces — never calls Application
- **API** is thin — controllers only read/write DTOs and call `IMediator`/`IBus`

## Domain Layer Rules

- All entities extend `AggregateRoot` or `Entity`
- All value objects extend `ValueObject`
- Domain events implement `IDomainEvent` and are raised inside aggregates via `AddDomainEvent()`
- No public setters on entities — use methods that encode business rules
- No static methods that mutate state
- No `async` in domain — domain is pure logic, no I/O

```csharp
// CORRECT
public class Invoice : AggregateRoot
{
    private readonly List<InvoiceItem> _items = [];

    public void AddItem(string description, decimal amount)
    {
        _items.Add(new InvoiceItem(description, amount));
        AddDomainEvent(new InvoiceItemAddedEvent(Id, description, amount));
    }
}

// WRONG — anytime you see this, fix it
public decimal Total { get; set; } // public setter
```

## Application Layer Rules

- One file per use case: `Commands/CreateInvoice/CreateInvoiceCommand.cs` + `CreateInvoiceHandler.cs`
- Commands return primitive or typed result — never domain entities
- Queries return DTOs — never domain entities
- Validators live next to commands: `CreateInvoiceValidator.cs`
- No business logic in handlers — delegate to domain aggregates or domain services
- No EF Core references — use repository interfaces from Domain

```csharp
// Command
public record CreateInvoiceCommand(int ClientId, List<InvoiceItemDto> Items);

// Handler
public class CreateInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    public async Task<int> Handle(CreateInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = Invoice.Create(cmd.ClientId, cmd.Items.Select(...));
        repo.Add(invoice);
        await uow.SaveChangesAsync(ct);
        return invoice.Id;
    }
}
```

## Infrastructure Layer Rules

- EF Core `DbContext`, migrations, and configurations live here only
- Repository implementations implement Domain interfaces
- External service clients (Stripe, Namecheap, cPanel) live in `Infrastructure/Integrations/`
- No business logic — only I/O, mapping, persistence

## API Layer Rules

- Controllers are thin — no logic, only dispatch to Wolverine bus
- Always use `[ApiController]` and route attributes
- Return `IActionResult` or typed `ActionResult<T>`
- Auth: use `[Authorize]` with roles — never inline claims checks in controllers
- No direct repository or DbContext injection in controllers

```csharp
[ApiController]
[Route("api/billing/invoices")]
public class InvoicesController(IMessageBus bus) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,Client")]
    public async Task<ActionResult<int>> Create(CreateInvoiceRequest req)
    {
        var id = await bus.InvokeAsync<int>(new CreateInvoiceCommand(req.ClientId, req.Items));
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }
}
```

## Wolverine Rules

- Commands: use `bus.InvokeAsync<TResult>()` — synchronous request/response
- Events: use `bus.PublishAsync()` — fire and forget, handled async
- Handlers are plain classes — no interface needed, convention-based
- All event handlers that do I/O must be idempotent (safe to retry)
- Use outbox for all cross-aggregate side effects

## Pluggable Interface Rules

- Payment gateways implement `IPaymentGateway`
- Registrar providers implement `IRegistrarProvider`
- Provisioning providers implement `IProvisioningProvider`
- Interfaces live in **Domain** — implementations in **Infrastructure**
- Never `new` a gateway directly — always inject the interface

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
        InvoiceDto.cs
    Events/
      InvoiceCreatedHandler.cs
```

## Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Command | `VerbNounCommand` | `CreateInvoiceCommand` |
| Query | `GetNounQuery` | `GetInvoiceQuery` |
| Handler | `VerbNounHandler` | `CreateInvoiceHandler` |
| Event | `NounVerbedEvent` | `InvoiceCreatedEvent` |
| DTO | `NounDto` | `InvoiceDto` |
| Repository interface | `INounRepository` | `IInvoiceRepository` |
| Repository impl | `NounRepository` | `InvoiceRepository` |
