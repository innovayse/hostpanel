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
- Extension classes: `<WhatItExtends>Extensions.cs`, in an `Extensions/` folder under the layer or
  feature that owns it — never appended to the DTO or controller it serves. Placement, layer
  ownership and the DI carve-out: [clean-architecture.md](clean-architecture.md)

```csharp
// File: src/Innovayse.Application/Billing/Commands/CreateInvoice/CreateInvoiceHandler.cs
namespace Innovayse.Application.Billing.Commands.CreateInvoice;
```

### GlobalUsings.cs

**Every project gets a `GlobalUsings.cs` at its root, and the usings a project needs in nearly
every file belong there rather than repeated at the top of two hundred of them.**

```csharp
// File: src/Innovayse.<Product>.Application/GlobalUsings.cs
global using FluentValidation;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
```

One per project, not one per solution — the set is different for each layer, and that is the
point. Domain needs almost nothing; Application needs the validation and logging namespaces;
Infrastructure needs EF Core; API needs the MVC ones. A namespace that belongs to one feature
does not go here: `global using` is for what a project uses *everywhere*, and a global using
that only three files need makes those three files harder to read, not easier, because the
reader can no longer see where the type came from.

This is not the same as `<ImplicitUsings>enable</ImplicitUsings>`, which adds a fixed SDK list
you do not control. Use both: implicit usings for the BCL, `GlobalUsings.cs` for the packages
and internal namespaces this project actually leans on.

**A `global using` is a dependency you can no longer see at the call site**, so keep the file
short and keep it sorted. If adding one to the file would let a layer reach something it should
not — EF Core in Application, an ASP.NET type in Domain — that is the file-layout rule speaking,
and the answer is not to hide the using.

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
