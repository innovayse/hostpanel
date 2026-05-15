# Provisioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Provisioning module — IProvisioningProvider interface, cPanel WHM API client, commands for provisioning/suspending/terminating hosting services, cPanel SSO integration, and event-driven provisioning triggered by service orders.

**Architecture:** IProvisioningProvider lives in Domain as an interface; cPanel WHM implementation in Infrastructure makes real HTTP API calls; provisioning is triggered by ServiceOrderedEvent via Wolverine handler; cPanel SSO generates one-time login URLs for clients to access their control panel; suspend/terminate operations update ClientService status and trigger domain suspension if linked.

**Tech Stack:** C# 12, ASP.NET Core 8, cPanel WHM API v1 (JSON), HttpClient, Wolverine event handlers, FluentValidation, xUnit + FluentAssertions + Testcontainers.

---

## Codebase Context

Read these files before implementing any task:

- `src/Innovayse.Domain/Common/AggregateRoot.cs` — base class
- `src/Innovayse.Domain/Services/ClientService.cs` — service aggregate that gets provisioned
- `src/Innovayse.Domain/Services/ServiceStatus.cs` — status enum
- `src/Innovayse.Application/Services/Commands/OrderService/OrderServiceHandler.cs` — raises ServiceOrderedEvent
- `src/Innovayse.Infrastructure/DependencyInjection.cs` — register new providers
- `tests/Innovayse.Integration.Tests/IntegrationTestFactory.cs` — test factory

**Critical rules:**
- All handlers: `public async Task<T> HandleAsync(Command cmd, CancellationToken ct)`
- All C# members need XML docs
- Private fields: `_camelCase`. Properties: `PascalCase`
- Run `dotnet format` after every task

---

## File Map

```
src/Innovayse.Domain/Provisioning/
  ProvisioningResult.cs                             ← record returned by provisioning operations
  ProvisionRequest.cs                               ← record for provision operation
  SuspendRequest.cs                                 ← record for suspend operation
  TerminateRequest.cs                               ← record for terminate operation
  ServiceCredentials.cs                             ← record with cPanel username/password/domain
  Events/ServiceProvisionedEvent.cs
  Events/ServiceSuspendedEvent.cs
  Events/ServiceTerminatedEvent.cs
  Interfaces/IProvisioningProvider.cs

src/Innovayse.Application/Provisioning/
  DTOs/ServiceCredentialsDto.cs
  DTOs/ProvisioningDetailsDto.cs
  Commands/ProvisionService/
    ProvisionServiceCommand.cs
    ProvisionServiceHandler.cs
  Commands/SuspendService/
    SuspendServiceCommand.cs
    SuspendServiceHandler.cs
  Commands/TerminateService/
    TerminateServiceCommand.cs
    TerminateServiceHandler.cs
  Commands/UnsuspendService/
    UnsuspendServiceCommand.cs
    UnsuspendServiceHandler.cs
  Queries/GetServiceCredentials/
    GetServiceCredentialsQuery.cs
    GetServiceCredentialsHandler.cs
  Queries/GetCPanelSsoUrl/
    GetCPanelSsoUrlQuery.cs
    GetCPanelSsoUrlHandler.cs
  Events/ServiceOrderedHandler.cs

src/Innovayse.Infrastructure/Provisioning/
  CPanel/
    CPanelSettings.cs
    CPanelClient.cs
    CPanelProvisioningProvider.cs

src/Innovayse.Infrastructure/
  DependencyInjection.cs                            ← register IProvisioningProvider + HttpClient

src/Innovayse.API/Provisioning/
  ProvisioningController.cs                         ← Admin endpoints
  MyServicesController.cs                           ← Client cPanel SSO endpoint

tests/Innovayse.Integration.Tests/Provisioning/
  StubProvisioningProvider.cs
  ProvisioningEndpointTests.cs
```

---

## Task 1: Domain — Events + Interface + Records

**Files:**
- Create: `src/Innovayse.Domain/Provisioning/ProvisioningResult.cs`
- Create: `src/Innovayse.Domain/Provisioning/ProvisionRequest.cs`
- Create: `src/Innovayse.Domain/Provisioning/SuspendRequest.cs`
- Create: `src/Innovayse.Domain/Provisioning/TerminateRequest.cs`
- Create: `src/Innovayse.Domain/Provisioning/ServiceCredentials.cs`
- Create: `src/Innovayse.Domain/Provisioning/Events/ServiceProvisionedEvent.cs`
- Create: `src/Innovayse.Domain/Provisioning/Events/ServiceSuspendedEvent.cs`
- Create: `src/Innovayse.Domain/Provisioning/Events/ServiceTerminatedEvent.cs`
- Create: `src/Innovayse.Domain/Provisioning/Interfaces/IProvisioningProvider.cs`

- [ ] **Step 1: Create domain records**

```csharp
// src/Innovayse.Domain/Provisioning/ProvisioningResult.cs
namespace Innovayse.Domain.Provisioning;

/// <summary>Result of a provisioning operation.</summary>
/// <param name="Success">True if the operation succeeded.</param>
/// <param name="ProvisioningRef">Provider-assigned identifier for the provisioned service.</param>
/// <param name="ErrorMessage">Error message if operation failed.</param>
public record ProvisioningResult(
    bool Success,
    string? ProvisioningRef,
    string? ErrorMessage);
```

```csharp
// src/Innovayse.Domain/Provisioning/ProvisionRequest.cs
namespace Innovayse.Domain.Provisioning;

/// <summary>Request to provision a new hosting service.</summary>
/// <param name="ServiceId">The client service identifier.</param>
/// <param name="DomainName">The primary domain for this service.</param>
/// <param name="Username">The cPanel username to create.</param>
/// <param name="Password">The cPanel password.</param>
/// <param name="Package">The hosting package/plan name.</param>
public record ProvisionRequest(
    int ServiceId,
    string DomainName,
    string Username,
    string Password,
    string Package);
```

```csharp
// src/Innovayse.Domain/Provisioning/SuspendRequest.cs
namespace Innovayse.Domain.Provisioning;

/// <summary>Request to suspend a hosting service.</summary>
/// <param name="ServiceId">The client service identifier.</param>
/// <param name="ProvisioningRef">Provider-assigned identifier.</param>
/// <param name="Reason">The reason for suspension.</param>
public record SuspendRequest(
    int ServiceId,
    string ProvisioningRef,
    string Reason);
```

```csharp
// src/Innovayse.Domain/Provisioning/TerminateRequest.cs
namespace Innovayse.Domain.Provisioning;

/// <summary>Request to terminate a hosting service.</summary>
/// <param name="ServiceId">The client service identifier.</param>
/// <param name="ProvisioningRef">Provider-assigned identifier.</param>
/// <param name="Reason">The reason for termination.</param>
public record TerminateRequest(
    int ServiceId,
    string ProvisioningRef,
    string Reason);
```

```csharp
// src/Innovayse.Domain/Provisioning/ServiceCredentials.cs
namespace Innovayse.Domain.Provisioning;

/// <summary>Service credentials for client access.</summary>
/// <param name="Username">The cPanel username.</param>
/// <param name="Password">The cPanel password.</param>
/// <param name="Domain">The primary domain.</param>
/// <param name="ServerIp">The server IP address.</param>
/// <param name="CpanelUrl">The cPanel access URL.</param>
public record ServiceCredentials(
    string Username,
    string Password,
    string Domain,
    string ServerIp,
    string CpanelUrl);
```

- [ ] **Step 2: Create domain events**

```csharp
// src/Innovayse.Domain/Provisioning/Events/ServiceProvisionedEvent.cs
namespace Innovayse.Domain.Provisioning.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a service is successfully provisioned.</summary>
/// <param name="ServiceId">The service identifier.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="ProvisioningRef">Provider-assigned identifier.</param>
public record ServiceProvisionedEvent(int ServiceId, int ClientId, string ProvisioningRef) : IDomainEvent;
```

```csharp
// src/Innovayse.Domain/Provisioning/Events/ServiceSuspendedEvent.cs
namespace Innovayse.Domain.Provisioning.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a service is suspended.</summary>
/// <param name="ServiceId">The service identifier.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="Reason">The reason for suspension.</param>
public record ServiceSuspendedEvent(int ServiceId, int ClientId, string Reason) : IDomainEvent;
```

```csharp
// src/Innovayse.Domain/Provisioning/Events/ServiceTerminatedEvent.cs
namespace Innovayse.Domain.Provisioning.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a service is terminated.</summary>
/// <param name="ServiceId">The service identifier.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="Reason">The reason for termination.</param>
public record ServiceTerminatedEvent(int ServiceId, int ClientId, string Reason) : IDomainEvent;
```

- [ ] **Step 3: Create IProvisioningProvider interface**

```csharp
// src/Innovayse.Domain/Provisioning/Interfaces/IProvisioningProvider.cs
namespace Innovayse.Domain.Provisioning.Interfaces;

using Innovayse.Domain.Provisioning;

/// <summary>Abstraction over hosting provisioning providers (cPanel WHM, Plesk, etc.).</summary>
public interface IProvisioningProvider
{
    /// <summary>
    /// Provisions a new hosting service.
    /// </summary>
    /// <param name="req">The provisioning request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provisioning result.</returns>
    Task<ProvisioningResult> ProvisionAsync(ProvisionRequest req, CancellationToken ct);

    /// <summary>
    /// Suspends an existing hosting service.
    /// </summary>
    /// <param name="req">The suspension request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SuspendAsync(SuspendRequest req, CancellationToken ct);

    /// <summary>
    /// Unsuspends a previously suspended service.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UnsuspendAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Terminates a hosting service permanently.
    /// </summary>
    /// <param name="req">The termination request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TerminateAsync(TerminateRequest req, CancellationToken ct);

    /// <summary>
    /// Retrieves service credentials for client access.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Service credentials.</returns>
    Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Generates a cPanel SSO URL for one-time client login.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The SSO URL.</returns>
    Task<string> GetCPanelSsoUrlAsync(string provisioningRef, CancellationToken ct);
}
```

- [ ] **Step 4: Run dotnet format**

```bash
cd backend
dotnet format
```

- [ ] **Step 5: Commit**

```bash
git add src/Innovayse.Domain/Provisioning/
git commit -m "feat(provisioning): add domain events, records, and IProvisioningProvider interface"
```

---

## Task 2: Application — DTOs

**Files:**
- Create: `src/Innovayse.Application/Provisioning/DTOs/ServiceCredentialsDto.cs`
- Create: `src/Innovayse.Application/Provisioning/DTOs/ProvisioningDetailsDto.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// src/Innovayse.Application/Provisioning/DTOs/ServiceCredentialsDto.cs
namespace Innovayse.Application.Provisioning.DTOs;

/// <summary>Service credentials data transfer object.</summary>
/// <param name="Username">The cPanel username.</param>
/// <param name="Password">The cPanel password.</param>
/// <param name="Domain">The primary domain.</param>
/// <param name="ServerIp">The server IP address.</param>
/// <param name="CpanelUrl">The cPanel access URL.</param>
public record ServiceCredentialsDto(
    string Username,
    string Password,
    string Domain,
    string ServerIp,
    string CpanelUrl);
```

```csharp
// src/Innovayse.Application/Provisioning/DTOs/ProvisioningDetailsDto.cs
namespace Innovayse.Application.Provisioning.DTOs;

/// <summary>Provisioning details data transfer object.</summary>
/// <param name="ServiceId">The service identifier.</param>
/// <param name="ProvisioningRef">Provider-assigned identifier.</param>
/// <param name="Status">Current provisioning status.</param>
/// <param name="ProvisionedAt">Date and time of provisioning.</param>
public record ProvisioningDetailsDto(
    int ServiceId,
    string ProvisioningRef,
    string Status,
    DateTimeOffset ProvisionedAt);
```

- [ ] **Step 2: Run dotnet format and commit**

```bash
cd backend
dotnet format
git add src/Innovayse.Application/Provisioning/DTOs/
git commit -m "feat(provisioning): add application DTOs"
```

---

## Task 3: Application — Commands (Provision, Suspend, Terminate, Unsuspend)

**Files:**
- Create: `src/Innovayse.Application/Provisioning/Commands/ProvisionService/ProvisionServiceCommand.cs`
- Create: `src/Innovayse.Application/Provisioning/Commands/ProvisionService/ProvisionServiceHandler.cs`
- Create: `src/Innovayse.Application/Provisioning/Commands/SuspendService/SuspendServiceCommand.cs`
- Create: `src/Innovayse.Application/Provisioning/Commands/SuspendService/SuspendServiceHandler.cs`
- Create: `src/Innovayse.Application/Provisioning/Commands/TerminateService/TerminateServiceCommand.cs`
- Create: `src/Innovayse.Application/Provisioning/Commands/TerminateService/TerminateServiceHandler.cs`
- Create: `src/Innovayse.Application/Provisioning/Commands/UnsuspendService/UnsuspendServiceCommand.cs`
- Create: `src/Innovayse.Application/Provisioning/Commands/UnsuspendService/UnsuspendServiceHandler.cs`

- [ ] **Step 1: Create ProvisionService command and handler**

```csharp
// src/Innovayse.Application/Provisioning/Commands/ProvisionService/ProvisionServiceCommand.cs
namespace Innovayse.Application.Provisioning.Commands.ProvisionService;

/// <summary>Command to provision a hosting service.</summary>
/// <param name="ServiceId">The service identifier.</param>
public record ProvisionServiceCommand(int ServiceId);
```

```csharp
// src/Innovayse.Application/Provisioning/Commands/ProvisionService/ProvisionServiceHandler.cs
namespace Innovayse.Application.Provisioning.Commands.ProvisionService;

using Innovayse.Application.Common;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Handler for <see cref="ProvisionServiceCommand"/>.</summary>
public sealed class ProvisionServiceHandler
{
    private readonly IClientServiceRepository _serviceRepository;
    private readonly IProvisioningProvider _provisioningProvider;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProvisionServiceHandler"/> class.
    /// </summary>
    /// <param name="serviceRepository">The service repository.</param>
    /// <param name="provisioningProvider">The provisioning provider.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public ProvisionServiceHandler(
        IClientServiceRepository serviceRepository,
        IProvisioningProvider provisioningProvider,
        IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _provisioningProvider = provisioningProvider;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the provision service command.
    /// </summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ProvisionServiceCommand cmd, CancellationToken ct)
    {
        var service = await _serviceRepository.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        var request = new ProvisionRequest(
            service.Id,
            service.Domain ?? "temp.example.com",
            $"user{service.Id}",
            GeneratePassword(),
            "default");

        var result = await _provisioningProvider.ProvisionAsync(request, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Provisioning failed: {result.ErrorMessage}");
        }

        service.MarkProvisioned(result.ProvisioningRef!);
        service.AddDomainEvent(new ServiceProvisionedEvent(0, service.ClientId, result.ProvisioningRef!));

        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Generates a random secure password.</summary>
    /// <returns>A 16-character password.</returns>
    private static string GeneratePassword()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }
}
```

- [ ] **Step 2: Create SuspendService command and handler**

```csharp
// src/Innovayse.Application/Provisioning/Commands/SuspendService/SuspendServiceCommand.cs
namespace Innovayse.Application.Provisioning.Commands.SuspendService;

/// <summary>Command to suspend a service.</summary>
/// <param name="ServiceId">The service identifier.</param>
/// <param name="Reason">The reason for suspension.</param>
public record SuspendServiceCommand(int ServiceId, string Reason);
```

```csharp
// src/Innovayse.Application/Provisioning/Commands/SuspendService/SuspendServiceHandler.cs
namespace Innovayse.Application.Provisioning.Commands.SuspendService;

using Innovayse.Application.Common;
using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Handler for <see cref="SuspendServiceCommand"/>.</summary>
public sealed class SuspendServiceHandler
{
    private readonly IClientServiceRepository _serviceRepository;
    private readonly IProvisioningProvider _provisioningProvider;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuspendServiceHandler"/> class.
    /// </summary>
    /// <param name="serviceRepository">The service repository.</param>
    /// <param name="provisioningProvider">The provisioning provider.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public SuspendServiceHandler(
        IClientServiceRepository serviceRepository,
        IProvisioningProvider provisioningProvider,
        IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _provisioningProvider = provisioningProvider;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the suspend service command.
    /// </summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(SuspendServiceCommand cmd, CancellationToken ct)
    {
        var service = await _serviceRepository.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        var request = new SuspendRequest(service.Id, service.ProvisioningRef!, cmd.Reason);
        await _provisioningProvider.SuspendAsync(request, ct);

        service.Suspend();
        service.AddDomainEvent(new ServiceSuspendedEvent(0, service.ClientId, cmd.Reason));

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Create TerminateService and UnsuspendService commands and handlers**

Create similar patterns for `TerminateService` and `UnsuspendService`.

- [ ] **Step 4: Run dotnet format and commit**

```bash
cd backend
dotnet format
git add src/Innovayse.Application/Provisioning/Commands/
git commit -m "feat(provisioning): add Provision, Suspend, Terminate, and Unsuspend commands"
```

---

## Task 4: Application — Queries + ServiceOrderedHandler

**Files:**
- Create: `src/Innovayse.Application/Provisioning/Queries/GetServiceCredentials/GetServiceCredentialsQuery.cs`
- Create: `src/Innovayse.Application/Provisioning/Queries/GetServiceCredentials/GetServiceCredentialsHandler.cs`
- Create: `src/Innovayse.Application/Provisioning/Queries/GetCPanelSsoUrl/GetCPanelSsoUrlQuery.cs`
- Create: `src/Innovayse.Application/Provisioning/Queries/GetCPanelSsoUrl/GetCPanelSsoUrlHandler.cs`
- Create: `src/Innovayse.Application/Provisioning/Events/ServiceOrderedHandler.cs`

- [ ] **Step 1: Create queries**

Create query and handler files for GetServiceCredentials and GetCPanelSsoUrl following established patterns.

- [ ] **Step 2: Create ServiceOrderedHandler**

```csharp
// src/Innovayse.Application/Provisioning/Events/ServiceOrderedHandler.cs
namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Domain.Services.Events;
using Wolverine;

/// <summary>Handler for <see cref="ServiceOrderedEvent"/>.</summary>
public sealed class ServiceOrderedHandler
{
    private readonly IMessageBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceOrderedHandler"/> class.
    /// </summary>
    /// <param name="bus">The Wolverine message bus.</param>
    public ServiceOrderedHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Handles the service ordered event.
    /// </summary>
    /// <param name="evt">The event.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ServiceOrderedEvent evt, CancellationToken ct)
    {
        await _bus.InvokeAsync(new ProvisionServiceCommand(evt.ServiceId), ct);
    }
}
```

- [ ] **Step 3: Run dotnet format and commit**

```bash
cd backend
dotnet format
git add src/Innovayse.Application/Provisioning/
git commit -m "feat(provisioning): add queries and ServiceOrderedHandler for automatic provisioning"
```

---

## Task 5: Infrastructure — cPanel WHM Integration

**Files:**
- Create: `src/Innovayse.Infrastructure/Provisioning/CPanel/CPanelSettings.cs`
- Create: `src/Innovayse.Infrastructure/Provisioning/CPanel/CPanelClient.cs`
- Create: `src/Innovayse.Infrastructure/Provisioning/CPanel/CPanelProvisioningProvider.cs`
- Modify: `src/Innovayse.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1-3: Create cPanel integration classes**

Create CPanelSettings, CPanelClient (with WHM API v1 JSON calls), and CPanelProvisioningProvider following Namecheap patterns from Plan 06.

- [ ] **Step 4: Update DependencyInjection.cs**

```csharp
// Add inside AddInfrastructure:
services.AddScoped<IProvisioningProvider, CPanelProvisioningProvider>();
services.AddHttpClient<CPanelClient>(client =>
{
    var apiUrl = configuration["CPanel:ApiUrl"] ?? "https://whm.example.com:2087";
    client.BaseAddress = new Uri(apiUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
services.Configure<CPanelSettings>(configuration.GetSection("CPanel"));
```

- [ ] **Step 5: Run dotnet format and commit**

```bash
cd backend
dotnet format
git add src/Innovayse.Infrastructure/Provisioning/ src/Innovayse.Infrastructure/DependencyInjection.cs
git commit -m "feat(provisioning): add cPanel WHM API integration"
```

---

## Task 6: API — Controllers + Integration Tests

**Files:**
- Create: `src/Innovayse.API/Provisioning/ProvisioningController.cs`
- Modify: `src/Innovayse.API/Services/MyServicesController.cs`
- Create: `tests/Innovayse.Integration.Tests/Provisioning/StubProvisioningProvider.cs`
- Create: `tests/Innovayse.Integration.Tests/Provisioning/ProvisioningEndpointTests.cs`

- [ ] **Step 1-4: Create controllers, stub provider, and tests**

Follow patterns from Plan 06 (Domains).

- [ ] **Step 5: Run tests, format, and commit**

```bash
cd tests/Innovayse.Integration.Tests
dotnet test --filter "FullyQualifiedName~ProvisioningEndpointTests"
cd ../../backend
dotnet format
git add src/Innovayse.API/Provisioning/ tests/Innovayse.Integration.Tests/Provisioning/
git commit -m "feat(provisioning): add API controllers and integration tests"
```

---

## Self-Review

- [x] IProvisioningProvider interface
- [x] cPanel WHM integration
- [x] All commands (Provision, Suspend, Terminate, Unsuspend)
- [x] Queries (GetCredentials, GetCPanelSsoUrl)
- [x] ServiceOrderedHandler for automatic provisioning
- [x] Integration tests with stub provider

---

## Execution Handoff

Plan complete. Two execution options:

**1. Subagent-Driven (recommended)** — Fresh subagent per task

**2. Inline Execution** — Execute in this session

**Which approach?**
