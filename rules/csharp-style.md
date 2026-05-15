# C# Code Style Rules — Innovayse Backend

## General

- C# 12+ features preferred: primary constructors, collection expressions, pattern matching
- `var` for local variables when type is obvious from the right-hand side
- `record` for DTOs, commands, queries, value objects
- `sealed` on classes that are not designed for inheritance
- No `null` — prefer `required`, nullable reference types, and guard clauses
- No `async void` — always `async Task`
- No `.Result` or `.Wait()` — always `await`

## required Properties

- All `required` properties MUST be PascalCase
- Always use `required` instead of constructor params for mandatory DTO/record init properties

```csharp
// CORRECT
public class CreateClientRequest
{
    public required string FirstName { get; init; }
    public required string LastName  { get; init; }
    public required string Email     { get; init; }
    public string? Phone             { get; init; }
}

// WRONG
public class CreateClientRequest
{
    public required string firstName { get; init; }  // camelCase — forbidden
    public string email;                             // field instead of property — forbidden
}
```

## Nullability

- Nullable reference types enabled project-wide (`<Nullable>enable</Nullable>`)
- No `!` null-forgiving operator except in tests
- Use `ArgumentNullException.ThrowIfNull()` at public API boundaries

## Primary Constructors (C# 12)

```csharp
// CORRECT
public class CreateInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    public async Task<int> Handle(CreateInvoiceCommand cmd, CancellationToken ct) { ... }
}

// AVOID (old style)
public class CreateInvoiceHandler
{
    private readonly IInvoiceRepository _repo;
    public CreateInvoiceHandler(IInvoiceRepository repo) { _repo = repo; }
}
```

## Records for Commands/Queries/DTOs

```csharp
public record CreateInvoiceCommand(int ClientId, List<InvoiceItemDto> Items);
public record InvoiceDto(int Id, decimal Total, string Status, DateTimeOffset CreatedAt);
```

## Collection Expressions (C# 12)

```csharp
// CORRECT
List<string> names = [];
int[] ids = [1, 2, 3];

// AVOID
var names = new List<string>();
```

## Pattern Matching

```csharp
// CORRECT
var message = status switch
{
    InvoiceStatus.Paid => "Invoice is paid",
    InvoiceStatus.Overdue => "Invoice is overdue",
    _ => "Unknown status"
};
```

## Async

- Method suffix `Async` on all async methods
- Always pass `CancellationToken ct` as the last parameter
- Always name it `ct` (not `cancellationToken`)

```csharp
public async Task<InvoiceDto> GetInvoiceAsync(int id, CancellationToken ct) { ... }
```

## Error Handling

- Domain errors: throw custom exceptions from Domain layer (e.g., `InvoiceNotFoundException`)
- API layer catches domain exceptions via global exception middleware — no try/catch in controllers
- No swallowing exceptions (`catch (Exception) { }`)

## Logging

- Use `ILogger<T>` injected via primary constructor
- Structured logging — no string interpolation in log messages

```csharp
// CORRECT
_logger.LogInformation("Invoice {InvoiceId} created for client {ClientId}", invoice.Id, clientId);

// WRONG
_logger.LogInformation($"Invoice {invoice.Id} created");
```

## File Organization

- One type per file
- File name matches type name exactly
- Namespace matches folder structure exactly

```csharp
// File: src/Innovayse.Application/Billing/Commands/CreateInvoice/CreateInvoiceHandler.cs
namespace Innovayse.Application.Billing.Commands.CreateInvoice;
```

## No Magic Numbers / Strings

```csharp
// CORRECT
private const int MaxInvoiceItems = 100;
private const string AdminRole = "Admin";

// WRONG
if (items.Count > 100) ...
[Authorize(Roles = "Admin")]  // OK only in attributes
```

## Dependency Injection

- Always inject interfaces, never concrete types (except in Infrastructure registrations)
- No `ServiceLocator` pattern — no `IServiceProvider` injection except in factories
- Register services in module extension methods:

```csharp
public static IServiceCollection AddBillingModule(this IServiceCollection services)
{
    services.AddScoped<IInvoiceRepository, InvoiceRepository>();
    return services;
}
```
