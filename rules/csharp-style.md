# C# Code Style Rules for the Hostpanel Backend

## General

- C# 12+ features preferred: primary constructors, collection expressions, pattern matching
- `var` for local variables when type is obvious from the right-hand side
- `record` for DTOs, commands, queries, value objects
- `sealed` on classes that are not designed for inheritance
- No `null`: prefer `required`, nullable reference types, and guard clauses
- No `async void`: always `async Task`
- No `.Result` or `.Wait()`: always `await`

## required Properties

- All `required` properties MUST be PascalCase
- Always use `required` instead of constructor params for mandatory DTO/record init properties

```csharp
// CORRECT
public sealed class ClientContactDto
{
    public required string FirstName { get; init; }
    public required string LastName  { get; init; }
    public required string Email     { get; init; }
    public string? Phone             { get; init; }
}

// WRONG
public sealed class ClientContactDto
{
    public required string firstName { get; init; }  // camelCase, forbidden
    public string email;                             // field instead of property, forbidden
}
```

## Nullability

- Nullable reference types enabled project-wide (`<Nullable>enable</Nullable>`)
- No `!` null-forgiving operator except in tests
- Use `ArgumentNullException.ThrowIfNull()` at public API boundaries

## Constructors

Handlers and other classes with dependencies use an explicit constructor and one documented
`private readonly` field per dependency. Primary constructors are for records and types with no
dependencies to explain.

```csharp
// CORRECT
public sealed class CreateInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CreateInvoiceHandler(IHostpanelDbContext context)
    {
        _context = context;
    }
}

// AVOID: no repositories, no IUnitOfWork, no undocumented primary-constructor dependencies
public class CreateInvoiceHandler(IInvoiceRepository repo, IUnitOfWork uow) { }
```

## Records for Commands/Queries/DTOs

```csharp
public sealed record CreateInvoiceCommand(
    int ClientId, DateTimeOffset DueDate, IReadOnlyList<CreateInvoiceItemDto> Items, bool IsDraft = false);
public sealed record InvoiceBriefDto(int Id, int ClientId, InvoiceStatus Status, decimal Total, string Currency);
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

- Domain refusals throw a `DomainException(code, message)` subclass (for example `DomainRuleException`);
  the code is a constant in the module's error-code class, never a bespoke `*NotFoundException`
- A handler that can foresee a refusal (not found, forbidden, conflict) returns `Result.Failure(code)`
  instead of throwing
- The exception middleware turns an uncaught `DomainException` into an `ErrorResponse` whose status
  comes from `StatusMapping.For(code)`: no try/catch in controllers
- No swallowing exceptions (`catch (Exception) { }`)

## Logging

- Inject `ILogger<T>` through the explicit constructor into a documented `private readonly` field
- Structured logging: no string interpolation in log messages

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
  feature that owns it, never appended to the DTO or controller it serves. Placement, layer
  ownership and the DI carve-out: [clean-architecture.md](clean-architecture.md)

```csharp
// File: src/Innovayse.Hostpanel.Application/Billing/Commands/CreateInvoice/CreateInvoiceHandler.cs
namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateInvoice;
```

### GlobalUsings.cs

**Every backend project has a `GlobalUsings.cs` at its root, and it holds only what that project
uses in nearly every file.** `<ImplicitUsings>enable</ImplicitUsings>` (set in
`Directory.Build.props`) covers the BCL; every other `using` is written at the top of the file
unless it is used project-wide. What each layer holds today:

- `Shared`: `Microsoft.AspNetCore.Http` and `Microsoft.Extensions.Logging`.
- `SharedKernel`, Domain, Application, Infrastructure, API, `Hostpanel.Sdk` and the
  `Innovayse.Providers.*` projects: no directives. In these projects each namespace is used by one
  folder only (controllers, EF configurations, handlers), not by the whole project, so the file
  stays empty except for its comment.
- Test projects: `FluentAssertions` and `Xunit`; `Integration.Tests` also has its own `Fixtures`
  namespace.

```csharp
// File: src/Shared/GlobalUsings.cs
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.Logging;
```

A namespace that belongs to one feature never goes here: a global using that only three files
need makes those files harder to read, because the reader can no longer see where the type came
from. **A `global using` is a dependency you can no longer see at the call site**, so keep the
file short and sorted, and never use it to let a layer reach something it should not: no EF Core
global using in Domain or Application, no ASP.NET type in Domain.

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
- No `ServiceLocator` pattern: no `IServiceProvider` injection except in factories
- Register services in the layer's `DependencyInjection.cs` extension method:

```csharp
// File: src/Innovayse.Hostpanel.Application/DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddValidatorsFromAssemblyContaining<AcceptInvitationCommand>();

    return services;
}
```
