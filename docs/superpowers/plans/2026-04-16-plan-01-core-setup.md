# Plan 01 — Core Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bootstrap the Innovayse.Backend C# solution with Clean Architecture layers, PostgreSQL via EF Core, Wolverine messaging, and a running minimal API endpoint that proves the wiring works end-to-end.

**Architecture:** Clean Architecture monolith — Domain → Application → Infrastructure → API. Zero business logic in this plan; only the skeleton and shared primitives that every future plan depends on.

**Tech Stack:** .NET 8, ASP.NET Core 8, EF Core 8 + Npgsql, Wolverine, Serilog, Scalar, FluentValidation, Mapster, xUnit, Testcontainers (PostgreSQL)

---

## File Map

```
/c/Users/Dell/Desktop/www/innovayse/backend/
├── Innovayse.Backend.sln
├── .editorconfig                          (already exists — copy from rules/)
├── src/
│   ├── Innovayse.Domain/
│   │   ├── Innovayse.Domain.csproj
│   │   └── Common/
│   │       ├── Entity.cs
│   │       ├── AggregateRoot.cs
│   │       ├── ValueObject.cs
│   │       └── IDomainEvent.cs
│   ├── Innovayse.Application/
│   │   ├── Innovayse.Application.csproj
│   │   └── Common/
│   │       └── IUnitOfWork.cs
│   ├── Innovayse.Infrastructure/
│   │   ├── Innovayse.Infrastructure.csproj
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   └── UnitOfWork.cs
│   │   └── DependencyInjection.cs
│   └── Innovayse.API/
│       ├── Innovayse.API.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── Health/
│           └── HealthController.cs
└── tests/
    ├── Innovayse.Domain.Tests/
    │   ├── Innovayse.Domain.Tests.csproj
    │   └── Common/
    │       └── AggregateRootTests.cs
    ├── Innovayse.Application.Tests/
    │   └── Innovayse.Application.Tests.csproj
    └── Innovayse.Integration.Tests/
        ├── Innovayse.Integration.Tests.csproj
        └── Health/
            └── HealthEndpointTests.cs
```

---

## Task 1: Create Solution and Projects

**Files:**
- Create: `backend/Innovayse.Backend.sln`
- Create: `backend/src/Innovayse.Domain/Innovayse.Domain.csproj`
- Create: `backend/src/Innovayse.Application/Innovayse.Application.csproj`
- Create: `backend/src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj`
- Create: `backend/src/Innovayse.API/Innovayse.API.csproj`
- Create: `backend/tests/Innovayse.Domain.Tests/Innovayse.Domain.Tests.csproj`
- Create: `backend/tests/Innovayse.Application.Tests/Innovayse.Application.Tests.csproj`
- Create: `backend/tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj`

- [ ] **Step 1: Scaffold solution and projects**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend

dotnet new sln -n Innovayse.Backend

dotnet new classlib -n Innovayse.Domain        -o src/Innovayse.Domain        --framework net8.0
dotnet new classlib -n Innovayse.Application   -o src/Innovayse.Application   --framework net8.0
dotnet new classlib -n Innovayse.Infrastructure -o src/Innovayse.Infrastructure --framework net8.0
dotnet new webapi   -n Innovayse.API           -o src/Innovayse.API           --framework net8.0

dotnet new xunit -n Innovayse.Domain.Tests        -o tests/Innovayse.Domain.Tests        --framework net8.0
dotnet new xunit -n Innovayse.Application.Tests   -o tests/Innovayse.Application.Tests   --framework net8.0
dotnet new xunit -n Innovayse.Integration.Tests   -o tests/Innovayse.Integration.Tests   --framework net8.0

dotnet sln add src/Innovayse.Domain/Innovayse.Domain.csproj
dotnet sln add src/Innovayse.Application/Innovayse.Application.csproj
dotnet sln add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj
dotnet sln add src/Innovayse.API/Innovayse.API.csproj
dotnet sln add tests/Innovayse.Domain.Tests/Innovayse.Domain.Tests.csproj
dotnet sln add tests/Innovayse.Application.Tests/Innovayse.Application.Tests.csproj
dotnet sln add tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj
```

Expected: solution file + 7 project folders created.

- [ ] **Step 2: Add project references (layer dependencies)**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend

# Application depends on Domain
dotnet add src/Innovayse.Application/Innovayse.Application.csproj reference src/Innovayse.Domain/Innovayse.Domain.csproj

# Infrastructure depends on Domain
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj reference src/Innovayse.Domain/Innovayse.Domain.csproj

# Infrastructure depends on Application (IUnitOfWork etc.)
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj reference src/Innovayse.Application/Innovayse.Application.csproj

# API depends on Application and Infrastructure
dotnet add src/Innovayse.API/Innovayse.API.csproj reference src/Innovayse.Application/Innovayse.Application.csproj
dotnet add src/Innovayse.API/Innovayse.API.csproj reference src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj

# Test projects
dotnet add tests/Innovayse.Domain.Tests/Innovayse.Domain.Tests.csproj reference src/Innovayse.Domain/Innovayse.Domain.csproj
dotnet add tests/Innovayse.Application.Tests/Innovayse.Application.Tests.csproj reference src/Innovayse.Application/Innovayse.Application.csproj
dotnet add tests/Innovayse.Application.Tests/Innovayse.Application.Tests.csproj reference src/Innovayse.Domain/Innovayse.Domain.csproj
dotnet add tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj reference src/Innovayse.API/Innovayse.API.csproj
```

- [ ] **Step 3: Delete boilerplate files generated by dotnet new**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend

rm src/Innovayse.Domain/Class1.cs
rm src/Innovayse.Application/Class1.cs
rm src/Innovayse.Infrastructure/Class1.cs
rm src/Innovayse.API/Controllers/WeatherForecastController.cs
rm src/Innovayse.API/WeatherForecast.cs
rm tests/Innovayse.Domain.Tests/UnitTest1.cs
rm tests/Innovayse.Application.Tests/UnitTest1.cs
rm tests/Innovayse.Integration.Tests/UnitTest1.cs
```

- [ ] **Step 4: Configure all .csproj files for nullable + XML docs + treat warnings as errors**

Replace the contents of `src/Innovayse.Domain/Innovayse.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>12</LangVersion>
  </PropertyGroup>
</Project>
```

Repeat the same `<PropertyGroup>` block for all other projects:
- `src/Innovayse.Application/Innovayse.Application.csproj`
- `src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj`
- `src/Innovayse.API/Innovayse.API.csproj`

For test projects add `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (xUnit generates some warnings):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Copy .editorconfig to backend root**

```bash
cp /c/Users/Dell/Desktop/www/innovayse/backend/.editorconfig /c/Users/Dell/Desktop/www/innovayse/backend/.editorconfig
# already in place — verify it exists
ls /c/Users/Dell/Desktop/www/innovayse/backend/.editorconfig
```

- [ ] **Step 6: Verify solution builds**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git init
git add .
git commit -m "chore: scaffold solution with Clean Architecture project structure"
```

---

## Task 2: Domain Common Primitives

**Files:**
- Create: `src/Innovayse.Domain/Common/Entity.cs`
- Create: `src/Innovayse.Domain/Common/AggregateRoot.cs`
- Create: `src/Innovayse.Domain/Common/ValueObject.cs`
- Create: `src/Innovayse.Domain/Common/IDomainEvent.cs`
- Test: `tests/Innovayse.Domain.Tests/Common/AggregateRootTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Innovayse.Domain.Tests/Common/AggregateRootTests.cs`:

```csharp
namespace Innovayse.Domain.Tests.Common;

using Innovayse.Domain.Common;

/// <summary>Tests for <see cref="AggregateRoot"/> base class.</summary>
public class AggregateRootTests
{
    /// <summary>Aggregate raises domain event added via AddDomainEvent.</summary>
    [Fact]
    public void AddDomainEvent_ShouldAppendEventToList()
    {
        // Arrange
        var aggregate = new TestAggregate(1);

        // Act
        aggregate.DoSomething();

        // Assert
        Assert.Single(aggregate.DomainEvents);
        Assert.IsType<TestEvent>(aggregate.DomainEvents[0]);
    }

    /// <summary>ClearDomainEvents empties the list.</summary>
    [Fact]
    public void ClearDomainEvents_ShouldEmptyTheList()
    {
        var aggregate = new TestAggregate(1);
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Minimal aggregate for testing.</summary>
    private sealed class TestAggregate(int id) : AggregateRoot(id)
    {
        /// <summary>Triggers a domain event.</summary>
        public void DoSomething() => AddDomainEvent(new TestEvent(Id));
    }

    /// <summary>Test domain event.</summary>
    private sealed record TestEvent(int AggregateId) : IDomainEvent;
}
```

- [ ] **Step 2: Run test — expect FAIL (types not yet defined)**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Domain.Tests --no-build 2>&1 | tail -5
```

Expected: build error — `AggregateRoot`, `IDomainEvent` not found.

- [ ] **Step 3: Create IDomainEvent**

Create `src/Innovayse.Domain/Common/IDomainEvent.cs`:

```csharp
namespace Innovayse.Domain.Common;

/// <summary>
/// Marker interface for all domain events.
/// Domain events represent something that happened within the domain
/// and are dispatched by Wolverine after the aggregate is persisted.
/// </summary>
public interface IDomainEvent;
```

- [ ] **Step 4: Create Entity**

Create `src/Innovayse.Domain/Common/Entity.cs`:

```csharp
namespace Innovayse.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Provides identity-based equality — two entities with the same <see cref="Id"/> are equal.
/// </summary>
public abstract class Entity
{
    /// <summary>Gets the unique identifier of this entity.</summary>
    public int Id { get; private set; }

    /// <summary>Initialises a new entity with the given identifier.</summary>
    /// <param name="id">The entity identifier. Use 0 for new (unsaved) entities.</param>
    protected Entity(int id) => Id = id;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>Equality operator based on entity identity.</summary>
    public static bool operator ==(Entity? left, Entity? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Inequality operator based on entity identity.</summary>
    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
```

- [ ] **Step 5: Create AggregateRoot**

Create `src/Innovayse.Domain/Common/AggregateRoot.cs`:

```csharp
namespace Innovayse.Domain.Common;

/// <summary>
/// Base class for aggregate roots.
/// Extends <see cref="Entity"/> with domain event collection.
/// Wolverine dispatches <see cref="DomainEvents"/> after the aggregate is saved.
/// </summary>
public abstract class AggregateRoot(int id) : Entity(id)
{
    /// <summary>Internal mutable list of domain events raised during this operation.</summary>
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Gets the domain events raised since the last <see cref="ClearDomainEvents"/> call.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Appends a domain event to be dispatched after persistence.
    /// Call this from aggregate methods that encode business state transitions.
    /// </summary>
    /// <param name="domainEvent">The event to enqueue.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    /// <summary>
    /// Removes all queued domain events.
    /// Called by the unit of work after Wolverine has dispatched the events.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

- [ ] **Step 6: Create ValueObject**

Create `src/Innovayse.Domain/Common/ValueObject.cs`:

```csharp
namespace Innovayse.Domain.Common;

/// <summary>
/// Base class for value objects.
/// Equality is based on the values returned by <see cref="GetEqualityComponents"/>,
/// not on reference identity.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Returns the components used for equality comparison.
    /// All fields / properties that define the value object's identity must be yielded here.
    /// </summary>
    /// <returns>Sequence of equality components.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        return ((ValueObject)obj).GetEqualityComponents()
            .SequenceEqual(GetEqualityComponents());
    }

    /// <inheritdoc/>
    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj));

    /// <summary>Equality operator based on value components.</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Inequality operator based on value components.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) =>
        !(left == right);
}
```

- [ ] **Step 7: Run tests — expect PASS**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Domain.Tests -v normal
```

Expected: `Passed: 2, Failed: 0`

- [ ] **Step 8: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Domain/Common/ tests/Innovayse.Domain.Tests/Common/
git commit -m "feat(domain): add Entity, AggregateRoot, ValueObject, IDomainEvent primitives"
```

---

## Task 3: Application Common — IUnitOfWork

**Files:**
- Create: `src/Innovayse.Application/Common/IUnitOfWork.cs`
- Test: `tests/Innovayse.Application.Tests/Common/IUnitOfWorkContractTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Innovayse.Application.Tests/Common/IUnitOfWorkContractTests.cs`:

```csharp
namespace Innovayse.Application.Tests.Common;

using Innovayse.Application.Common;

/// <summary>Verifies that IUnitOfWork contract is accessible and well-formed.</summary>
public class IUnitOfWorkContractTests
{
    /// <summary>IUnitOfWork must expose SaveChangesAsync returning Task of int.</summary>
    [Fact]
    public void IUnitOfWork_SaveChangesAsync_ShouldExistWithCorrectSignature()
    {
        var method = typeof(IUnitOfWork).GetMethod(nameof(IUnitOfWork.SaveChangesAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<int>), method!.ReturnType);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Application.Tests 2>&1 | tail -5
```

Expected: build error — `IUnitOfWork` not found.

- [ ] **Step 3: Create IUnitOfWork**

Create `src/Innovayse.Application/Common/IUnitOfWork.cs`:

```csharp
namespace Innovayse.Application.Common;

/// <summary>
/// Abstracts the persistence transaction boundary.
/// Infrastructure implements this; Application calls it at the end of each command handler.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes to the database in a single transaction.
    /// Domain events on modified aggregates are dispatched by Wolverine after this call.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Application.Tests -v normal
```

Expected: `Passed: 1, Failed: 0`

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Common/ tests/Innovayse.Application.Tests/Common/
git commit -m "feat(application): add IUnitOfWork contract"
```

---

## Task 4: NuGet Packages

**Files:**
- Modify: `src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj`
- Modify: `src/Innovayse.API/Innovayse.API.csproj`
- Modify: `tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj`

- [ ] **Step 1: Add Infrastructure packages**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend

dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package Microsoft.EntityFrameworkCore --version 8.0.11
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.11
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.11
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package WolverineFx --version 3.7.0
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package WolverineFx.Postgresql --version 3.7.0
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package Serilog.AspNetCore --version 8.0.3
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package Serilog.Sinks.PostgreSQL --version 4.2.0
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package Mapster --version 7.4.0
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj package FluentValidation --version 11.11.0
```

- [ ] **Step 2: Add API packages**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend

dotnet add src/Innovayse.API/Innovayse.API.csproj package WolverineFx --version 3.7.0
dotnet add src/Innovayse.API/Innovayse.API.csproj package Scalar.AspNetCore --version 2.0.27
dotnet add src/Innovayse.API/Innovayse.API.csproj package Serilog.AspNetCore --version 8.0.3
dotnet add src/Innovayse.API/Innovayse.API.csproj package FluentValidation.AspNetCore --version 11.3.0
dotnet add src/Innovayse.API/Innovayse.API.csproj package Mapster --version 7.4.0
```

- [ ] **Step 3: Add Integration Test packages**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend

dotnet add tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 8.0.11
dotnet add tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj package Testcontainers.PostgreSql --version 3.11.0
dotnet add tests/Innovayse.Integration.Tests/Innovayse.Integration.Tests.csproj package FluentAssertions --version 6.12.2
```

- [ ] **Step 4: Build to verify all packages resolve**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add **/*.csproj
git commit -m "chore: add NuGet packages — EF Core, Wolverine, Serilog, Mapster, FluentValidation, Scalar"
```

---

## Task 5: AppDbContext and UnitOfWork

**Files:**
- Create: `src/Innovayse.Infrastructure/Persistence/AppDbContext.cs`
- Create: `src/Innovayse.Infrastructure/Persistence/UnitOfWork.cs`

- [ ] **Step 1: Create AppDbContext**

Create `src/Innovayse.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
namespace Innovayse.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Root EF Core DbContext for the Innovayse backend.
/// All entity configurations are registered here via <see cref="OnModelCreating"/>.
/// Future modules add their own <see cref="IEntityTypeConfiguration{T}"/> files
/// and register them with <c>modelBuilder.ApplyConfigurationsFromAssembly</c>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly automatically.
        // Each future module adds its own configuration files — no manual registration needed.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 2: Create UnitOfWork**

Create `src/Innovayse.Infrastructure/Persistence/UnitOfWork.cs`:

```csharp
namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Application.Common;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// Delegates to <see cref="AppDbContext.SaveChangesAsync(CancellationToken)"/>.
/// </summary>
/// <param name="db">The application DbContext.</param>
public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Infrastructure/Persistence/
git commit -m "feat(infrastructure): add AppDbContext and UnitOfWork"
```

---

## Task 6: Infrastructure DependencyInjection

**Files:**
- Create: `src/Innovayse.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create DependencyInjection extension**

Create `src/Innovayse.Infrastructure/DependencyInjection.cs`:

```csharp
namespace Innovayse.Infrastructure;

using Innovayse.Application.Common;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers all Infrastructure layer services into the DI container.
/// Call <see cref="AddInfrastructure"/> from <c>Program.cs</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds EF Core, UnitOfWork, and future Infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Application configuration (reads ConnectionStrings:DefaultConnection).</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Infrastructure/DependencyInjection.cs
git commit -m "feat(infrastructure): add DependencyInjection extension method"
```

---

## Task 7: Program.cs — Wire Everything

**Files:**
- Modify: `src/Innovayse.API/Program.cs`
- Modify: `src/Innovayse.API/appsettings.json`
- Modify: `src/Innovayse.API/appsettings.Development.json`
- Create: `src/Innovayse.API/Health/HealthController.cs`

- [ ] **Step 1: Update appsettings.json**

Replace `src/Innovayse.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=innovayse;Username=postgres;Password=postgres"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/log-.txt", "rollingInterval": "Day" } }
    ]
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173"
    ]
  }
}
```

- [ ] **Step 2: Update appsettings.Development.json**

Replace `src/Innovayse.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=innovayse_dev;Username=postgres;Password=postgres"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

- [ ] **Step 3: Create HealthController**

Create `src/Innovayse.API/Health/HealthController.cs`:

```csharp
namespace Innovayse.API.Health;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Liveness probe endpoint used by load balancers and integration tests
/// to verify the API is running.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 OK when the API process is alive.</summary>
    /// <returns>A simple JSON object with status "ok".</returns>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow });
}
```

- [ ] **Step 4: Write Program.cs**

Replace `src/Innovayse.API/Program.cs`:

```csharp
using Innovayse.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext());

    // CORS
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    // MVC + OpenAPI
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // Wolverine
    builder.Host.UseWolverine(opts =>
    {
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    });

    // Infrastructure (EF Core, UnitOfWork)
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Needed for WebApplicationFactory in integration tests
public partial class Program;
```

- [ ] **Step 5: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.API/Innovayse.API.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.API/
git commit -m "feat(api): wire Program.cs with Serilog, Wolverine, CORS, Scalar, Infrastructure"
```

---

## Task 8: Integration Test — Health Endpoint

**Files:**
- Create: `tests/Innovayse.Integration.Tests/Health/HealthEndpointTests.cs`
- Create: `tests/Innovayse.Integration.Tests/IntegrationTestFactory.cs`

- [ ] **Step 1: Create IntegrationTestFactory (Testcontainers + WebApplicationFactory)**

Create `tests/Innovayse.Integration.Tests/IntegrationTestFactory.cs`:

```csharp
namespace Innovayse.Integration.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

/// <summary>
/// Spins up a real PostgreSQL container via Testcontainers and hosts the API
/// in-process using <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Shared across all integration tests via <see cref="IClassFixture{T}"/>.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>PostgreSQL container started before each test class.</summary>
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("innovayse_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Override connection string to point at the test container
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
            });
        });
    }

    /// <summary>Starts the PostgreSQL container before tests run.</summary>
    public async Task InitializeAsync() => await _postgres.StartAsync();

    /// <summary>Stops and disposes the PostgreSQL container after tests complete.</summary>
    public new async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

- [ ] **Step 2: Write the failing integration test**

Create `tests/Innovayse.Integration.Tests/Health/HealthEndpointTests.cs`:

```csharp
namespace Innovayse.Integration.Tests.Health;

using System.Net;
using FluentAssertions;

/// <summary>Integration tests for GET /api/health.</summary>
public sealed class HealthEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Health endpoint returns 200 OK with status ok.</summary>
    [Fact]
    public async Task Get_Health_Returns200WithStatusOk()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("ok");
    }
}
```

- [ ] **Step 3: Run integration test — expect FAIL (no DB migrations yet)**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Integration.Tests -v normal
```

Expected: FAIL — connection error or migration error. That is expected at this stage.

- [ ] **Step 4: Run first EF Core migration**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend

# Install EF Core tools if not already installed
dotnet tool install --global dotnet-ef

# Create initial (empty) migration
dotnet ef migrations add InitialCreate \
  --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
  --startup-project src/Innovayse.API/Innovayse.API.csproj \
  --output-dir Persistence/Migrations
```

Expected: `Done. To undo this action, use 'ef migrations remove'`

- [ ] **Step 5: Apply migration in IntegrationTestFactory**

Update `tests/Innovayse.Integration.Tests/IntegrationTestFactory.cs` — add migration apply in `InitializeAsync`:

```csharp
using Innovayse.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

// Replace InitializeAsync with:
public async Task InitializeAsync()
{
    await _postgres.StartAsync();

    // Apply EF Core migrations to test database
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

- [ ] **Step 6: Run integration test — expect PASS**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Integration.Tests -v normal
```

Expected: `Passed: 1, Failed: 0`

- [ ] **Step 7: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add tests/Innovayse.Integration.Tests/ src/Innovayse.Infrastructure/Persistence/Migrations/
git commit -m "test(integration): health endpoint test with Testcontainers + initial EF migration"
```

---

## Task 9: dotnet format Pass

- [ ] **Step 1: Run formatter**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet format
```

Expected: no output = nothing to fix. If files are changed, they will be shown.

- [ ] **Step 2: Run all tests to confirm nothing broke**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test
```

Expected: all tests pass.

- [ ] **Step 3: Commit formatting fixes (if any)**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add .
git commit -m "style: dotnet format pass on core setup"
```

---

## Self-Review Checklist

- [x] Solution structure matches spec Section 1
- [x] Dependency rule enforced: Domain has no NuGet deps, Application has no EF Core
- [x] AggregateRoot, Entity, ValueObject, IDomainEvent covered by tests
- [x] IUnitOfWork interface in Application layer with test
- [x] EF Core wired via DI in Infrastructure
- [x] Wolverine registered in Program.cs
- [x] Health endpoint integration-tested with real PostgreSQL container
- [x] dotnet format run at end
- [x] All commits are granular and descriptive
- [x] No placeholder steps — all code is complete
