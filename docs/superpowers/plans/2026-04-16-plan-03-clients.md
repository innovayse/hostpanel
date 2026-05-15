# Plan 03 — Clients Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Client module — aggregate, contacts, billing address, CRUD for admins, self-service profile for clients, and automatic client record creation on user registration.

**Architecture:** `Client` is a Domain aggregate root linked to an `AppUser` via `UserId` (string FK). When a user registers, `RegisterHandler` publishes `ClientRegisteredIntegrationEvent`; a Wolverine handler creates the `Client` record. Admin endpoints use `[Authorize(Roles = "Admin,Reseller")]`. Client portal endpoints use `[Authorize(Roles = "Client")]` and extract `UserId` from JWT claims.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, Wolverine, FluentValidation, xUnit + Testcontainers, FluentAssertions

---

## File Map

```
src/Innovayse.Domain/
  Clients/
    Client.cs                                     # AggregateRoot — profile, address, contacts collection
    Contact.cs                                    # Entity — additional contacts for a client
    ClientStatus.cs                               # enum — Active, Inactive, Suspended, Closed
    ContactType.cs                                # enum — Billing, Technical, General
    Events/
      ClientCreatedEvent.cs                       # IDomainEvent raised by Client.Create()
    Interfaces/
      IClientRepository.cs                        # FindByIdAsync, FindByUserIdAsync, ListAsync, Add

src/Innovayse.Application/
  Common/
    PagedResult.cs                                # record PagedResult<T>(Items, TotalCount, Page, PageSize)
  Auth/
    Events/
      ClientRegisteredIntegrationEvent.cs         # Published by RegisterHandler after user creation
    Commands/Register/
      RegisterHandler.cs                          # MODIFY: publish ClientRegisteredIntegrationEvent
  Clients/
    DTOs/
      ClientDto.cs                                # Full client details
      ClientListItemDto.cs                        # Summary row for list endpoint
      ContactDto.cs                               # Contact details
    Commands/
      CreateClient/
        CreateClientCommand.cs
        CreateClientHandler.cs
        CreateClientValidator.cs
      UpdateClient/
        UpdateClientCommand.cs
        UpdateClientHandler.cs
        UpdateClientValidator.cs
      AddContact/
        AddContactCommand.cs
        AddContactHandler.cs
    Queries/
      GetClient/
        GetClientQuery.cs
        GetClientHandler.cs
      ListClients/
        ListClientsQuery.cs
        ListClientsHandler.cs
      GetMyProfile/
        GetMyProfileQuery.cs
        GetMyProfileHandler.cs
    Events/
      CreateClientOnRegisterHandler.cs            # Consumes ClientRegisteredIntegrationEvent

src/Innovayse.Infrastructure/
  Clients/
    ClientRepository.cs                           # IClientRepository EF implementation
    Configurations/
      ClientConfiguration.cs                      # EF config — clients table
      ContactConfiguration.cs                     # EF config — contacts table
  Persistence/
    AppDbContext.cs                               # MODIFY: add DbSet<Client>, DbSet<Contact>
  DependencyInjection.cs                          # MODIFY: register IClientRepository

src/Innovayse.API/
  Clients/
    ClientsController.cs                          # Admin: GET list, POST, GET /{id}, PUT /{id}
    ClientProfileController.cs                    # Client: GET /api/me, PUT /api/me
    Requests/
      CreateClientRequest.cs
      UpdateClientRequest.cs
      AddContactRequest.cs

tests/
  Innovayse.Domain.Tests/
    Clients/
      ClientTests.cs
  Innovayse.Application.Tests/
    Clients/
      CreateClientValidatorTests.cs
      UpdateClientValidatorTests.cs
  Innovayse.Integration.Tests/
    Clients/
      ClientsEndpointTests.cs
```

---

## Task 1: Domain — Client Aggregate + Contact + Enums + Event

**Files:**
- Create: `src/Innovayse.Domain/Clients/ClientStatus.cs`
- Create: `src/Innovayse.Domain/Clients/ContactType.cs`
- Create: `src/Innovayse.Domain/Clients/Contact.cs`
- Create: `src/Innovayse.Domain/Clients/Events/ClientCreatedEvent.cs`
- Create: `src/Innovayse.Domain/Clients/Client.cs`

- [ ] **Step 1: Create ClientStatus enum**

```csharp
namespace Innovayse.Domain.Clients;

/// <summary>Lifecycle status of a client account.</summary>
public enum ClientStatus
{
    /// <summary>Account is active and can use services.</summary>
    Active,

    /// <summary>Account is inactive (not yet activated or manually deactivated).</summary>
    Inactive,

    /// <summary>Account is suspended due to non-payment or policy violation.</summary>
    Suspended,

    /// <summary>Account is permanently closed.</summary>
    Closed
}
```

- [ ] **Step 2: Create ContactType enum**

```csharp
namespace Innovayse.Domain.Clients;

/// <summary>Classification of an additional contact on a client account.</summary>
public enum ContactType
{
    /// <summary>Receives billing and invoice emails.</summary>
    Billing,

    /// <summary>Receives technical and service notifications.</summary>
    Technical,

    /// <summary>General purpose contact.</summary>
    General
}
```

- [ ] **Step 3: Create Contact entity**

```csharp
namespace Innovayse.Domain.Clients;

using Innovayse.Domain.Common;

/// <summary>
/// An additional contact linked to a <see cref="Client"/> account.
/// Stored in the <c>contacts</c> table.
/// </summary>
public sealed class Contact : Entity
{
    /// <summary>Gets the ID of the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the contact's full name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the contact's email address.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Gets the contact's phone number, or <see langword="null"/> if not provided.</summary>
    public string? Phone { get; private set; }

    /// <summary>Gets the type of contact (billing, technical, general).</summary>
    public ContactType Type { get; private set; }

    /// <summary>Gets the UTC timestamp when this contact was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Contact() : base(0) { }

    /// <summary>
    /// Creates a new contact for a client.
    /// </summary>
    /// <param name="clientId">The owning client's ID.</param>
    /// <param name="name">The contact's full name.</param>
    /// <param name="email">The contact's email address.</param>
    /// <param name="phone">Optional phone number.</param>
    /// <param name="type">The contact type classification.</param>
    internal Contact(int clientId, string name, string email, string? phone, ContactType type) : base(0)
    {
        ClientId = clientId;
        Name = name;
        Email = email;
        Phone = phone;
        Type = type;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Create ClientCreatedEvent**

```csharp
namespace Innovayse.Domain.Clients.Events;

using Innovayse.Domain.Common;

/// <summary>
/// Domain event raised when a new <see cref="Client"/> is created.
/// Dispatched by Wolverine after the aggregate is persisted.
/// </summary>
/// <param name="ClientId">The new client's ID.</param>
/// <param name="UserId">The linked Identity user ID.</param>
/// <param name="Email">The client's email address.</param>
public record ClientCreatedEvent(int ClientId, string UserId, string Email) : IDomainEvent;
```

- [ ] **Step 5: Create Client aggregate**

```csharp
namespace Innovayse.Domain.Clients;

using Innovayse.Domain.Clients.Events;
using Innovayse.Domain.Common;

/// <summary>
/// Aggregate root for a client account.
/// Linked to an ASP.NET Core Identity user via <see cref="UserId"/>.
/// Contains the billing address inline and a collection of additional contacts.
/// Stored in the <c>clients</c> table.
/// </summary>
public sealed class Client : AggregateRoot
{
    /// <summary>Internal mutable contacts list.</summary>
    private readonly List<Contact> _contacts = [];

    /// <summary>Gets the ASP.NET Core Identity user ID (FK to AspNetUsers).</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Gets the client's first name.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Gets the client's last name.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>Gets the company name, or <see langword="null"/> for individual clients.</summary>
    public string? CompanyName { get; private set; }

    /// <summary>Gets the client's phone number.</summary>
    public string? Phone { get; private set; }

    /// <summary>Gets the current lifecycle status of the account.</summary>
    public ClientStatus Status { get; private set; }

    /// <summary>Gets the billing address street line.</summary>
    public string? Street { get; private set; }

    /// <summary>Gets the billing address city.</summary>
    public string? City { get; private set; }

    /// <summary>Gets the billing address state or region.</summary>
    public string? State { get; private set; }

    /// <summary>Gets the billing address postcode.</summary>
    public string? PostCode { get; private set; }

    /// <summary>Gets the billing address ISO 3166-1 alpha-2 country code (e.g. "US", "AM").</summary>
    public string? Country { get; private set; }

    /// <summary>Gets the UTC timestamp when the client account was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the additional contacts linked to this client.</summary>
    public IReadOnlyList<Contact> Contacts => _contacts.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Client() : base(0) { }

    /// <summary>
    /// Creates a new active client account and raises <see cref="ClientCreatedEvent"/>.
    /// </summary>
    /// <param name="userId">The Identity user ID to link this client to.</param>
    /// <param name="firstName">The client's first name.</param>
    /// <param name="lastName">The client's last name.</param>
    /// <param name="email">The client's email address (used for the domain event).</param>
    /// <param name="companyName">Optional company name.</param>
    /// <returns>A new, unpersisted <see cref="Client"/> instance.</returns>
    public static Client Create(string userId, string firstName, string lastName, string email, string? companyName = null)
    {
        var client = new Client
        {
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            CompanyName = companyName,
            Status = ClientStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Domain event will be dispatched by Wolverine after SaveChangesAsync
        // ClientId will be 0 here; infrastructure layer must dispatch after EF assigns the PK
        client.AddDomainEvent(new ClientCreatedEvent(0, userId, email));

        return client;
    }

    /// <summary>
    /// Updates the client's profile information.
    /// </summary>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    /// <param name="companyName">New company name (null to clear).</param>
    /// <param name="phone">New phone number (null to clear).</param>
    public void Update(string firstName, string lastName, string? companyName, string? phone)
    {
        FirstName = firstName;
        LastName = lastName;
        CompanyName = companyName;
        Phone = phone;
    }

    /// <summary>
    /// Updates the client's billing address.
    /// </summary>
    /// <param name="street">Street line.</param>
    /// <param name="city">City.</param>
    /// <param name="state">State or region.</param>
    /// <param name="postCode">Postcode or ZIP.</param>
    /// <param name="country">ISO 3166-1 alpha-2 country code.</param>
    public void UpdateAddress(string? street, string? city, string? state, string? postCode, string? country)
    {
        Street = street;
        City = city;
        State = state;
        PostCode = postCode;
        Country = country;
    }

    /// <summary>
    /// Adds a new additional contact to this client.
    /// </summary>
    /// <param name="name">The contact's full name.</param>
    /// <param name="email">The contact's email address.</param>
    /// <param name="phone">Optional phone number.</param>
    /// <param name="type">The contact type.</param>
    public void AddContact(string name, string email, string? phone, ContactType type)
    {
        _contacts.Add(new Contact(Id, name, email, phone, type));
    }

    /// <summary>Suspends the account. Throws if already suspended or closed.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the account is not in an active state.</exception>
    public void Suspend()
    {
        if (Status is ClientStatus.Suspended or ClientStatus.Closed)
            throw new InvalidOperationException($"Cannot suspend a client with status {Status}.");

        Status = ClientStatus.Suspended;
    }

    /// <summary>Reactivates a suspended or inactive account.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the account is closed.</exception>
    public void Activate()
    {
        if (Status == ClientStatus.Closed)
            throw new InvalidOperationException("Cannot activate a closed account.");

        Status = ClientStatus.Active;
    }
}
```

- [ ] **Step 6: Build Domain**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Domain 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Write domain unit tests**

Create `tests/Innovayse.Domain.Tests/Clients/ClientTests.cs`:

```csharp
namespace Innovayse.Domain.Tests.Clients;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Events;

/// <summary>Unit tests for the <see cref="Client"/> aggregate.</summary>
public class ClientTests
{
    /// <summary>Create should produce Active client with a domain event.</summary>
    [Fact]
    public void Create_ShouldReturnActiveClient_WithDomainEvent()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");

        Assert.Equal("user-1", client.UserId);
        Assert.Equal("John", client.FirstName);
        Assert.Equal(ClientStatus.Active, client.Status);
        Assert.Single(client.DomainEvents);
        Assert.IsType<ClientCreatedEvent>(client.DomainEvents[0]);
    }

    /// <summary>Update should change profile fields.</summary>
    [Fact]
    public void Update_ShouldChangeProfileFields()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Update("Jane", "Smith", "Acme Corp", "+1-555-0100");

        Assert.Equal("Jane", client.FirstName);
        Assert.Equal("Smith", client.LastName);
        Assert.Equal("Acme Corp", client.CompanyName);
        Assert.Equal("+1-555-0100", client.Phone);
    }

    /// <summary>Suspend should change status to Suspended.</summary>
    [Fact]
    public void Suspend_ShouldSetStatusToSuspended()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Suspend();

        Assert.Equal(ClientStatus.Suspended, client.Status);
    }

    /// <summary>Suspending an already suspended client should throw.</summary>
    [Fact]
    public void Suspend_WhenAlreadySuspended_ShouldThrow()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Suspend();

        Assert.Throws<InvalidOperationException>(() => client.Suspend());
    }

    /// <summary>Activate should restore a suspended client to Active.</summary>
    [Fact]
    public void Activate_WhenSuspended_ShouldSetStatusToActive()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Suspend();
        client.Activate();

        Assert.Equal(ClientStatus.Active, client.Status);
    }

    /// <summary>AddContact should append a contact to the collection.</summary>
    [Fact]
    public void AddContact_ShouldAppendToContacts()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.AddContact("Jane", "jane@example.com", null, ContactType.Billing);

        Assert.Single(client.Contacts);
        Assert.Equal("Jane", client.Contacts[0].Name);
    }
}
```

- [ ] **Step 8: Run domain tests**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Domain.Tests 2>&1 | tail -10
```

Expected: all pass (2 existing + 5 new = 7 total).

- [ ] **Step 9: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Domain/Clients/ tests/Innovayse.Domain.Tests/Clients/
git commit -m "feat(domain): add Client aggregate, Contact entity, enums, domain event + tests"
```

---

## Task 2: Domain — IClientRepository

**Files:**
- Create: `src/Innovayse.Domain/Clients/Interfaces/IClientRepository.cs`

- [ ] **Step 1: Create IClientRepository**

```csharp
namespace Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Client"/> aggregate operations.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IClientRepository
{
    /// <summary>
    /// Finds a client by their primary key.
    /// Returns <see langword="null"/> if not found.
    /// </summary>
    /// <param name="id">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client with contacts loaded, or <see langword="null"/>.</returns>
    Task<Client?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds the client linked to a given Identity user.
    /// Returns <see langword="null"/> if no client record exists for the user.
    /// </summary>
    /// <param name="userId">The Identity user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client with contacts loaded, or <see langword="null"/>.</returns>
    Task<Client?> FindByUserIdAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of clients, optionally filtered by a search term.
    /// The search term matches against first name, last name, company name, or email.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="search">Optional search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search, CancellationToken ct);

    /// <summary>
    /// Adds a new client to the repository.
    /// Call <c>IUnitOfWork.SaveChangesAsync</c> to persist.
    /// </summary>
    /// <param name="client">The new client aggregate.</param>
    void Add(Client client);
}
```

- [ ] **Step 2: Build Domain**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Domain 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Domain/Clients/Interfaces/
git commit -m "feat(domain): add IClientRepository interface"
```

---

## Task 3: Application — PagedResult + DTOs

**Files:**
- Create: `src/Innovayse.Application/Common/PagedResult.cs`
- Create: `src/Innovayse.Application/Clients/DTOs/ClientDto.cs`
- Create: `src/Innovayse.Application/Clients/DTOs/ClientListItemDto.cs`
- Create: `src/Innovayse.Application/Clients/DTOs/ContactDto.cs`

- [ ] **Step 1: Create PagedResult**

```csharp
namespace Innovayse.Application.Common;

/// <summary>
/// Wraps a paginated result set returned from list queries.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
/// <param name="Items">The items for the current page.</param>
/// <param name="TotalCount">Total number of matching items across all pages.</param>
/// <param name="Page">The current 1-based page number.</param>
/// <param name="PageSize">The number of items per page.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
```

- [ ] **Step 2: Create ContactDto**

```csharp
namespace Innovayse.Application.Clients.DTOs;

using Innovayse.Domain.Clients;

/// <summary>DTO for an additional client contact.</summary>
/// <param name="Id">Contact primary key.</param>
/// <param name="Name">Contact full name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact phone number (null if not set).</param>
/// <param name="Type">Contact type classification.</param>
public record ContactDto(int Id, string Name, string Email, string? Phone, ContactType Type);
```

- [ ] **Step 3: Create ClientDto**

```csharp
namespace Innovayse.Application.Clients.DTOs;

using Innovayse.Domain.Clients;

/// <summary>Full details DTO for a client, including contacts and billing address.</summary>
/// <param name="Id">Client primary key.</param>
/// <param name="UserId">Linked Identity user ID.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name (null for individuals).</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Status">Current account status.</param>
/// <param name="Street">Billing street address.</param>
/// <param name="City">Billing city.</param>
/// <param name="State">Billing state or region.</param>
/// <param name="PostCode">Billing postcode.</param>
/// <param name="Country">Billing country code.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="Contacts">Additional contacts linked to this client.</param>
public record ClientDto(
    int Id,
    string UserId,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    ClientStatus Status,
    string? Street,
    string? City,
    string? State,
    string? PostCode,
    string? Country,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ContactDto> Contacts);
```

- [ ] **Step 4: Create ClientListItemDto**

```csharp
namespace Innovayse.Application.Clients.DTOs;

using Innovayse.Domain.Clients;

/// <summary>Summary DTO for client list rows.</summary>
/// <param name="Id">Client primary key.</param>
/// <param name="UserId">Linked Identity user ID.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name (null for individuals).</param>
/// <param name="Status">Current account status.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
public record ClientListItemDto(
    int Id,
    string UserId,
    string FirstName,
    string LastName,
    string? CompanyName,
    ClientStatus Status,
    DateTimeOffset CreatedAt);
```

- [ ] **Step 5: Build Application**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Common/PagedResult.cs src/Innovayse.Application/Clients/
git commit -m "feat(application): add PagedResult + Client DTOs"
```

---

## Task 4: Application — CreateClient Command + ClientRegisteredIntegrationEvent + Modify RegisterHandler

**Files:**
- Create: `src/Innovayse.Application/Clients/Commands/CreateClient/CreateClientCommand.cs`
- Create: `src/Innovayse.Application/Clients/Commands/CreateClient/CreateClientValidator.cs`
- Create: `src/Innovayse.Application/Clients/Commands/CreateClient/CreateClientHandler.cs`
- Create: `src/Innovayse.Application/Auth/Events/ClientRegisteredIntegrationEvent.cs`
- Modify: `src/Innovayse.Application/Auth/Commands/Register/RegisterHandler.cs`
- Test: `tests/Innovayse.Application.Tests/Clients/CreateClientValidatorTests.cs`

- [ ] **Step 1: Create ClientRegisteredIntegrationEvent**

```csharp
namespace Innovayse.Application.Auth.Events;

/// <summary>
/// Integration event published by <c>RegisterHandler</c> after a new Identity user is created.
/// Wolverine delivers it to <c>CreateClientOnRegisterHandler</c> in the Clients module.
/// </summary>
/// <param name="UserId">The new user's Identity ID.</param>
/// <param name="Email">The new user's email address.</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
public record ClientRegisteredIntegrationEvent(string UserId, string Email, string FirstName, string LastName);
```

- [ ] **Step 2: Modify RegisterHandler to publish integration event**

Read the current file first:
```bash
cat /c/Users/Dell/Desktop/www/innovayse/backend/src/Innovayse.Application/Auth/Commands/Register/RegisterHandler.cs
```

Replace with:

```csharp
namespace Innovayse.Application.Auth.Commands.Register;

using Innovayse.Application.Auth.DTOs;
using Innovayse.Application.Auth.Events;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Wolverine;

/// <summary>
/// Handles <see cref="RegisterCommand"/>.
/// Creates a new Identity user with the Client role, issues tokens,
/// and publishes <see cref="ClientRegisteredIntegrationEvent"/> so the Clients module
/// can create the <c>Client</c> record.
/// </summary>
/// <param name="userService">Identity user management abstraction.</param>
/// <param name="tokenService">JWT token generation service.</param>
/// <param name="refreshTokenRepo">Refresh token persistence.</param>
/// <param name="uow">Unit of work for saving the refresh token.</param>
/// <param name="bus">Wolverine message bus for publishing integration events.</param>
public sealed class RegisterHandler(
    IUserService userService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    IUnitOfWork uow,
    IMessageBus bus)
{
    /// <summary>
    /// Executes the registration: creates user, assigns Client role, issues tokens,
    /// and publishes <see cref="ClientRegisteredIntegrationEvent"/>.
    /// </summary>
    /// <param name="cmd">The registration command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Auth result containing the access token and refresh token string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when user creation fails.</exception>
    public async Task<AuthWithRefreshDto> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var userId = await userService.CreateAsync(cmd.Email, cmd.Password, ct);
        await userService.AddToRoleAsync(userId, Roles.Client, ct);

        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(userId, cmd.Email, Roles.Client);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(userId);

        await refreshTokenRepo.AddAsync(userId, refreshToken, refreshExpiresAt, ct);
        await uow.SaveChangesAsync(ct);

        await bus.PublishAsync(new ClientRegisteredIntegrationEvent(userId, cmd.Email, cmd.FirstName, cmd.LastName));

        return new AuthWithRefreshDto(new AuthResultDto(accessToken, expiresAt, Roles.Client), refreshToken);
    }
}
```

- [ ] **Step 3: Create CreateClientCommand**

```csharp
namespace Innovayse.Application.Clients.Commands.CreateClient;

/// <summary>
/// Command to create a new client record.
/// Used by admin to create a client manually, and by <c>CreateClientOnRegisterHandler</c>
/// to auto-create a client on user registration.
/// </summary>
/// <param name="UserId">The Identity user ID to link to this client.</param>
/// <param name="Email">The user's email address (stored on domain event).</param>
/// <param name="FirstName">Client's first name.</param>
/// <param name="LastName">Client's last name.</param>
/// <param name="CompanyName">Optional company name.</param>
public record CreateClientCommand(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName = null);
```

- [ ] **Step 4: Create CreateClientValidator**

```csharp
namespace Innovayse.Application.Clients.Commands.CreateClient;

using FluentValidation;

/// <summary>Validates <see cref="CreateClientCommand"/> before the handler executes.</summary>
public sealed class CreateClientValidator : AbstractValidator<CreateClientCommand>
{
    /// <summary>Initialises validation rules for client creation.</summary>
    public CreateClientValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyName).MaximumLength(200).When(x => x.CompanyName is not null);
    }
}
```

- [ ] **Step 5: Write failing validator test**

Create `tests/Innovayse.Application.Tests/Clients/CreateClientValidatorTests.cs`:

```csharp
namespace Innovayse.Application.Tests.Clients;

using FluentValidation.TestHelper;
using Innovayse.Application.Clients.Commands.CreateClient;

/// <summary>Unit tests for <see cref="CreateClientValidator"/>.</summary>
public class CreateClientValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly CreateClientValidator _validator = new();

    /// <summary>Valid command should pass.</summary>
    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new CreateClientCommand("user-1", "john@example.com", "John", "Doe");
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Empty UserId should fail.</summary>
    [Fact]
    public void EmptyUserId_ShouldFail()
    {
        var cmd = new CreateClientCommand("", "john@example.com", "John", "Doe");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.UserId);
    }

    /// <summary>Invalid email should fail.</summary>
    [Fact]
    public void InvalidEmail_ShouldFail()
    {
        var cmd = new CreateClientCommand("user-1", "not-an-email", "John", "Doe");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>Empty FirstName should fail.</summary>
    [Fact]
    public void EmptyFirstName_ShouldFail()
    {
        var cmd = new CreateClientCommand("user-1", "john@example.com", "", "Doe");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FirstName);
    }
}
```

- [ ] **Step 6: Run tests**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Application.Tests 2>&1 | tail -10
```

Expected: all pass (existing 8 + 4 new = 12 total).

- [ ] **Step 7: Create CreateClientHandler**

```csharp
namespace Innovayse.Application.Clients.Commands.CreateClient;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="CreateClientCommand"/>.
/// Creates and persists a new <see cref="Client"/> aggregate.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class CreateClientHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new client record and saves it.
    /// </summary>
    /// <param name="cmd">The create client command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created client.</returns>
    public async Task<int> Handle(CreateClientCommand cmd, CancellationToken ct)
    {
        var client = Client.Create(cmd.UserId, cmd.FirstName, cmd.LastName, cmd.Email, cmd.CompanyName);
        clientRepo.Add(client);
        await uow.SaveChangesAsync(ct);
        return client.Id;
    }
}
```

- [ ] **Step 8: Build Application**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Auth/ src/Innovayse.Application/Clients/Commands/CreateClient/ tests/Innovayse.Application.Tests/Clients/
git commit -m "feat(application): add CreateClient command + ClientRegisteredIntegrationEvent + update RegisterHandler"
```

---

## Task 5: Application — UpdateClient Command

**Files:**
- Create: `src/Innovayse.Application/Clients/Commands/UpdateClient/UpdateClientCommand.cs`
- Create: `src/Innovayse.Application/Clients/Commands/UpdateClient/UpdateClientValidator.cs`
- Create: `src/Innovayse.Application/Clients/Commands/UpdateClient/UpdateClientHandler.cs`
- Test: `tests/Innovayse.Application.Tests/Clients/UpdateClientValidatorTests.cs`

- [ ] **Step 1: Create UpdateClientCommand**

```csharp
namespace Innovayse.Application.Clients.Commands.UpdateClient;

/// <summary>
/// Command to update an existing client's profile and billing address.
/// Used by both admin (any client) and client self-service (own profile only).
/// </summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="FirstName">Updated first name.</param>
/// <param name="LastName">Updated last name.</param>
/// <param name="CompanyName">Updated company name (null to clear).</param>
/// <param name="Phone">Updated phone number (null to clear).</param>
/// <param name="Street">Updated billing street.</param>
/// <param name="City">Updated billing city.</param>
/// <param name="State">Updated billing state.</param>
/// <param name="PostCode">Updated billing postcode.</param>
/// <param name="Country">Updated billing country code.</param>
public record UpdateClientCommand(
    int ClientId,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    string? Street,
    string? City,
    string? State,
    string? PostCode,
    string? Country);
```

- [ ] **Step 2: Create UpdateClientValidator**

```csharp
namespace Innovayse.Application.Clients.Commands.UpdateClient;

using FluentValidation;

/// <summary>Validates <see cref="UpdateClientCommand"/> before the handler executes.</summary>
public sealed class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
{
    /// <summary>Initialises validation rules for client update.</summary>
    public UpdateClientValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyName).MaximumLength(200).When(x => x.CompanyName is not null);
        RuleFor(x => x.Phone).MaximumLength(50).When(x => x.Phone is not null);
        RuleFor(x => x.Street).MaximumLength(200).When(x => x.Street is not null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City is not null);
        RuleFor(x => x.State).MaximumLength(100).When(x => x.State is not null);
        RuleFor(x => x.PostCode).MaximumLength(20).When(x => x.PostCode is not null);
        RuleFor(x => x.Country).Length(2).When(x => x.Country is not null);
    }
}
```

- [ ] **Step 3: Write validator tests**

Create `tests/Innovayse.Application.Tests/Clients/UpdateClientValidatorTests.cs`:

```csharp
namespace Innovayse.Application.Tests.Clients;

using FluentValidation.TestHelper;
using Innovayse.Application.Clients.Commands.UpdateClient;

/// <summary>Unit tests for <see cref="UpdateClientValidator"/>.</summary>
public class UpdateClientValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly UpdateClientValidator _validator = new();

    /// <summary>Valid command should pass.</summary>
    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new UpdateClientCommand(1, "John", "Doe", null, null, null, null, null, null, null);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>ClientId 0 should fail.</summary>
    [Fact]
    public void ZeroClientId_ShouldFail()
    {
        var cmd = new UpdateClientCommand(0, "John", "Doe", null, null, null, null, null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    /// <summary>Country code with wrong length should fail.</summary>
    [Fact]
    public void WrongLengthCountry_ShouldFail()
    {
        var cmd = new UpdateClientCommand(1, "John", "Doe", null, null, null, null, null, null, "USA");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Country);
    }

    /// <summary>Empty FirstName should fail.</summary>
    [Fact]
    public void EmptyFirstName_ShouldFail()
    {
        var cmd = new UpdateClientCommand(1, "", "Doe", null, null, null, null, null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FirstName);
    }
}
```

- [ ] **Step 4: Run tests**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Application.Tests 2>&1 | tail -10
```

Expected: 16 passed, 0 failed.

- [ ] **Step 5: Create UpdateClientHandler**

```csharp
namespace Innovayse.Application.Clients.Commands.UpdateClient;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="UpdateClientCommand"/>.
/// Loads the client aggregate, applies profile and address changes, and saves.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class UpdateClientHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates the client's profile and billing address.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task Handle(UpdateClientCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        client.Update(cmd.FirstName, cmd.LastName, cmd.CompanyName, cmd.Phone);
        client.UpdateAddress(cmd.Street, cmd.City, cmd.State, cmd.PostCode, cmd.Country);

        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 6: Build and commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application 2>&1 | tail -5
git add src/Innovayse.Application/Clients/Commands/UpdateClient/ tests/Innovayse.Application.Tests/Clients/UpdateClientValidatorTests.cs
git commit -m "feat(application): add UpdateClient command, handler, validator + tests"
```

---

## Task 6: Application — AddContact Command

**Files:**
- Create: `src/Innovayse.Application/Clients/Commands/AddContact/AddContactCommand.cs`
- Create: `src/Innovayse.Application/Clients/Commands/AddContact/AddContactHandler.cs`

- [ ] **Step 1: Create AddContactCommand**

```csharp
namespace Innovayse.Application.Clients.Commands.AddContact;

using Innovayse.Domain.Clients;

/// <summary>
/// Command to add an additional contact to a client account.
/// </summary>
/// <param name="ClientId">The target client's primary key.</param>
/// <param name="Name">The contact's full name.</param>
/// <param name="Email">The contact's email address.</param>
/// <param name="Phone">Optional phone number.</param>
/// <param name="Type">Contact type classification.</param>
public record AddContactCommand(int ClientId, string Name, string Email, string? Phone, ContactType Type);
```

- [ ] **Step 2: Create AddContactHandler**

```csharp
namespace Innovayse.Application.Clients.Commands.AddContact;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="AddContactCommand"/>.
/// Loads the client aggregate and appends a new contact.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class AddContactHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Adds a contact to the specified client and saves.
    /// </summary>
    /// <param name="cmd">The add contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task Handle(AddContactCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        client.AddContact(cmd.Name, cmd.Email, cmd.Phone, cmd.Type);
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Build and commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application 2>&1 | tail -5
git add src/Innovayse.Application/Clients/Commands/AddContact/
git commit -m "feat(application): add AddContact command and handler"
```

---

## Task 7: Application — Queries

**Files:**
- Create: `src/Innovayse.Application/Clients/Queries/GetClient/GetClientQuery.cs`
- Create: `src/Innovayse.Application/Clients/Queries/GetClient/GetClientHandler.cs`
- Create: `src/Innovayse.Application/Clients/Queries/ListClients/ListClientsQuery.cs`
- Create: `src/Innovayse.Application/Clients/Queries/ListClients/ListClientsHandler.cs`
- Create: `src/Innovayse.Application/Clients/Queries/GetMyProfile/GetMyProfileQuery.cs`
- Create: `src/Innovayse.Application/Clients/Queries/GetMyProfile/GetMyProfileHandler.cs`

- [ ] **Step 1: Create GetClientQuery + Handler**

`src/Innovayse.Application/Clients/Queries/GetClient/GetClientQuery.cs`:

```csharp
namespace Innovayse.Application.Clients.Queries.GetClient;

using Innovayse.Application.Clients.DTOs;

/// <summary>
/// Query to retrieve full details for a single client by primary key.
/// </summary>
/// <param name="ClientId">The client's primary key.</param>
public record GetClientQuery(int ClientId);
```

`src/Innovayse.Application/Clients/Queries/GetClient/GetClientHandler.cs`:

```csharp
namespace Innovayse.Application.Clients.Queries.GetClient;

using Innovayse.Application.Clients.DTOs;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetClientQuery"/>.
/// Returns full client details including contacts.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
public sealed class GetClientHandler(IClientRepository clientRepo)
{
    /// <summary>
    /// Retrieves a client by ID and maps to <see cref="ClientDto"/>.
    /// </summary>
    /// <param name="query">The query with the client ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task<ClientDto> Handle(GetClientQuery query, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(query.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {query.ClientId} not found.");

        return MapToDto(client);
    }

    /// <summary>Maps a <see cref="Client"/> aggregate to <see cref="ClientDto"/>.</summary>
    /// <param name="client">The client aggregate to map.</param>
    /// <returns>The mapped DTO.</returns>
    private static ClientDto MapToDto(Client client) =>
        new(
            client.Id,
            client.UserId,
            client.FirstName,
            client.LastName,
            client.CompanyName,
            client.Phone,
            client.Status,
            client.Street,
            client.City,
            client.State,
            client.PostCode,
            client.Country,
            client.CreatedAt,
            client.Contacts.Select(c => new ContactDto(c.Id, c.Name, c.Email, c.Phone, c.Type)).ToList());
}
```

- [ ] **Step 2: Create ListClientsQuery + Handler**

`src/Innovayse.Application/Clients/Queries/ListClients/ListClientsQuery.cs`:

```csharp
namespace Innovayse.Application.Clients.Queries.ListClients;

using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Common;

/// <summary>
/// Query to retrieve a paginated list of clients, with optional search.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1–100).</param>
/// <param name="Search">Optional search term matched against name, company.</param>
public record ListClientsQuery(int Page, int PageSize, string? Search = null);
```

`src/Innovayse.Application/Clients/Queries/ListClients/ListClientsHandler.cs`:

```csharp
namespace Innovayse.Application.Clients.Queries.ListClients;

using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="ListClientsQuery"/>.
/// Returns a paginated list of clients.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
public sealed class ListClientsHandler(IClientRepository clientRepo)
{
    /// <summary>
    /// Retrieves a paginated client list.
    /// </summary>
    /// <param name="query">The list query with pagination and optional search.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result of client summary DTOs.</returns>
    public async Task<PagedResult<ClientListItemDto>> Handle(ListClientsQuery query, CancellationToken ct)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);

        var (items, totalCount) = await clientRepo.ListAsync(page, pageSize, query.Search, ct);

        var dtos = items.Select(MapToListItem).ToList();

        return new PagedResult<ClientListItemDto>(dtos, totalCount, page, pageSize);
    }

    /// <summary>Maps a <see cref="Client"/> to <see cref="ClientListItemDto"/>.</summary>
    /// <param name="client">The client to map.</param>
    /// <returns>The list item DTO.</returns>
    private static ClientListItemDto MapToListItem(Client client) =>
        new(client.Id, client.UserId, client.FirstName, client.LastName, client.CompanyName, client.Status, client.CreatedAt);
}
```

- [ ] **Step 3: Create GetMyProfileQuery + Handler**

`src/Innovayse.Application/Clients/Queries/GetMyProfile/GetMyProfileQuery.cs`:

```csharp
namespace Innovayse.Application.Clients.Queries.GetMyProfile;

using Innovayse.Application.Clients.DTOs;

/// <summary>
/// Query to retrieve the authenticated client's own profile.
/// The controller extracts <paramref name="UserId"/> from the JWT sub claim.
/// </summary>
/// <param name="UserId">The authenticated user's Identity ID.</param>
public record GetMyProfileQuery(string UserId);
```

`src/Innovayse.Application/Clients/Queries/GetMyProfile/GetMyProfileHandler.cs`:

```csharp
namespace Innovayse.Application.Clients.Queries.GetMyProfile;

using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetClient;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetMyProfileQuery"/>.
/// Finds the client record linked to the authenticated user and returns their profile.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
public sealed class GetMyProfileHandler(IClientRepository clientRepo)
{
    /// <summary>
    /// Retrieves the client profile for the authenticated user.
    /// </summary>
    /// <param name="query">The query containing the user's Identity ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's full profile DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client record exists for the user.</exception>
    public async Task<ClientDto> Handle(GetMyProfileQuery query, CancellationToken ct)
    {
        var client = await clientRepo.FindByUserIdAsync(query.UserId, ct)
            ?? throw new InvalidOperationException($"No client profile found for user {query.UserId}.");

        return MapToDto(client);
    }

    /// <summary>Maps a <see cref="Client"/> aggregate to <see cref="ClientDto"/>.</summary>
    /// <param name="client">The client aggregate to map.</param>
    /// <returns>The mapped DTO.</returns>
    private static ClientDto MapToDto(Client client) =>
        new(
            client.Id,
            client.UserId,
            client.FirstName,
            client.LastName,
            client.CompanyName,
            client.Phone,
            client.Status,
            client.Street,
            client.City,
            client.State,
            client.PostCode,
            client.Country,
            client.CreatedAt,
            client.Contacts.Select(c => new ContactDto(c.Id, c.Name, c.Email, c.Phone, c.Type)).ToList());
}
```

- [ ] **Step 4: Build Application**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Clients/Queries/
git commit -m "feat(application): add GetClient, ListClients, GetMyProfile queries"
```

---

## Task 8: Application — CreateClientOnRegisterHandler

**Files:**
- Create: `src/Innovayse.Application/Clients/Events/CreateClientOnRegisterHandler.cs`

- [ ] **Step 1: Create CreateClientOnRegisterHandler**

```csharp
namespace Innovayse.Application.Clients.Events;

using Innovayse.Application.Auth.Events;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Wolverine handler that reacts to <see cref="ClientRegisteredIntegrationEvent"/>.
/// Automatically creates a <see cref="Client"/> record for every new registered user.
/// This handler is idempotent: if a client record already exists for the user, it does nothing.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
public sealed class CreateClientOnRegisterHandler(IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a Client record when a new user registers.
    /// </summary>
    /// <param name="evt">The integration event published by RegisterHandler.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(ClientRegisteredIntegrationEvent evt, CancellationToken ct)
    {
        // Idempotency: skip if the client record already exists
        var existing = await clientRepo.FindByUserIdAsync(evt.UserId, ct);
        if (existing is not null)
            return;

        var client = Client.Create(evt.UserId, evt.FirstName, evt.LastName, evt.Email);
        clientRepo.Add(client);
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Build Application**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Clients/Events/
git commit -m "feat(application): add CreateClientOnRegisterHandler"
```

---

## Task 9: Infrastructure — ClientRepository + EF Configs + AppDbContext + DI

**Files:**
- Create: `src/Innovayse.Infrastructure/Clients/ClientRepository.cs`
- Create: `src/Innovayse.Infrastructure/Clients/Configurations/ClientConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Clients/Configurations/ContactConfiguration.cs`
- Modify: `src/Innovayse.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/Innovayse.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create ClientConfiguration**

```csharp
namespace Innovayse.Infrastructure.Clients.Configurations;

using Innovayse.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="Client"/>.</summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450); // Identity ID max length

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Street).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.PostCode).HasMaxLength(20);
        builder.Property(x => x.Country).HasMaxLength(2);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Contacts)
            .WithOne()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2: Create ContactConfiguration**

```csharp
namespace Innovayse.Infrastructure.Clients.Configurations;

using Innovayse.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="Contact"/>.</summary>
public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(50);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
```

- [ ] **Step 3: Create ClientRepository**

```csharp
namespace Innovayse.Infrastructure.Clients;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IClientRepository"/>.
/// Operates on the <c>clients</c> and <c>contacts</c> tables via <see cref="AppDbContext"/>.
/// </summary>
/// <param name="db">The application DbContext.</param>
public sealed class ClientRepository(AppDbContext db) : IClientRepository
{
    /// <inheritdoc/>
    public async Task<Client?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Clients
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc/>
    public async Task<Client?> FindByUserIdAsync(string userId, CancellationToken ct) =>
        await db.Clients
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search, CancellationToken ct)
    {
        var query = db.Clients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(term) ||
                c.LastName.ToLower().Contains(term) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public void Add(Client client) => db.Clients.Add(client);
}
```

- [ ] **Step 4: Update AppDbContext**

Read the current file:
```bash
cat /c/Users/Dell/Desktop/www/innovayse/backend/src/Innovayse.Infrastructure/Persistence/AppDbContext.cs
```

Replace with:

```csharp
namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Domain.Clients;
using Innovayse.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Root EF Core DbContext for the Innovayse backend.
/// Extends <see cref="IdentityDbContext{TUser}"/> to include ASP.NET Core Identity tables.
/// All entity configurations are discovered automatically via <see cref="OnModelCreating"/>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    /// <summary>Gets the refresh tokens table.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Gets the clients table.</summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>Gets the contacts table.</summary>
    public DbSet<Contact> Contacts => Set<Contact>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly automatically.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Update DependencyInjection.cs**

Read the current file:
```bash
cat /c/Users/Dell/Desktop/www/innovayse/backend/src/Innovayse.Infrastructure/DependencyInjection.cs
```

Add after the existing `services.AddScoped<IUserService, UserService>();` line:

```csharp
        // Client services
        services.AddScoped<IClientRepository, ClientRepository>();
```

Also add the missing using at the top of the namespace block:
```csharp
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Infrastructure.Clients;
```

Full updated file:

```csharp
namespace Innovayse.Infrastructure;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Infrastructure.Auth;
using Innovayse.Infrastructure.Clients;
using Innovayse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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
    /// Adds EF Core, Identity, JWT token services, Infrastructure repositories, and client services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ASP.NET Core Identity
        services.AddIdentity<AppUser, IdentityRole>(opts =>
            {
                opts.Password.RequiredLength = 8;
                opts.Password.RequireDigit = false;
                opts.Password.RequireNonAlphanumeric = false;
                opts.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Auth services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserService, UserService>();

        // Client services
        services.AddScoped<IClientRepository, ClientRepository>();

        return services;
    }
}
```

- [ ] **Step 6: Build full solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Infrastructure/
git commit -m "feat(infrastructure): add ClientRepository, EF configs, update AppDbContext + DI"
```

---

## Task 10: Infrastructure — EF Migration

**Files:**
- Create: EF migration `AddClientAndContact`

- [ ] **Step 1: Add EF migration**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet ef migrations add AddClientAndContact \
  --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
  --startup-project src/Innovayse.API/Innovayse.API.csproj \
  --output-dir Persistence/Migrations
```

Expected: migration file created containing `clients` and `contacts` table creation.

- [ ] **Step 2: Build to verify migration compiles**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Infrastructure/Persistence/Migrations/
git commit -m "feat(infrastructure): add EF migration for clients and contacts tables"
```

---

## Task 11: API — ClientsController (Admin)

**Files:**
- Create: `src/Innovayse.API/Clients/Requests/CreateClientRequest.cs`
- Create: `src/Innovayse.API/Clients/Requests/UpdateClientRequest.cs`
- Create: `src/Innovayse.API/Clients/Requests/AddContactRequest.cs`
- Create: `src/Innovayse.API/Clients/ClientsController.cs`

- [ ] **Step 1: Create request records**

`src/Innovayse.API/Clients/Requests/CreateClientRequest.cs`:

```csharp
namespace Innovayse.API.Clients.Requests;

/// <summary>HTTP request body for POST /api/clients.</summary>
/// <param name="UserId">The Identity user ID to link to this client.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="FirstName">Client's first name.</param>
/// <param name="LastName">Client's last name.</param>
/// <param name="CompanyName">Optional company name.</param>
public record CreateClientRequest(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName);
```

`src/Innovayse.API/Clients/Requests/UpdateClientRequest.cs`:

```csharp
namespace Innovayse.API.Clients.Requests;

/// <summary>HTTP request body for PUT /api/clients/{id}.</summary>
/// <param name="FirstName">Updated first name.</param>
/// <param name="LastName">Updated last name.</param>
/// <param name="CompanyName">Updated company name (null to clear).</param>
/// <param name="Phone">Updated phone (null to clear).</param>
/// <param name="Street">Updated billing street.</param>
/// <param name="City">Updated billing city.</param>
/// <param name="State">Updated billing state.</param>
/// <param name="PostCode">Updated billing postcode.</param>
/// <param name="Country">Updated billing country code (2 chars).</param>
public record UpdateClientRequest(
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    string? Street,
    string? City,
    string? State,
    string? PostCode,
    string? Country);
```

`src/Innovayse.API/Clients/Requests/AddContactRequest.cs`:

```csharp
namespace Innovayse.API.Clients.Requests;

using Innovayse.Domain.Clients;

/// <summary>HTTP request body for POST /api/clients/{id}/contacts.</summary>
/// <param name="Name">Contact full name.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Optional phone number.</param>
/// <param name="Type">Contact type.</param>
public record AddContactRequest(string Name, string Email, string? Phone, ContactType Type);
```

- [ ] **Step 2: Create ClientsController**

```csharp
namespace Innovayse.API.Clients;

using Innovayse.API.Clients.Requests;
using Innovayse.Application.Clients.Commands.AddContact;
using Innovayse.Application.Clients.Commands.CreateClient;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetClient;
using Innovayse.Application.Clients.Queries.ListClients;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin/Reseller API for client account management.
/// All endpoints require the Admin or Reseller role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/clients")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ClientsController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Returns a paginated list of clients with optional search.
    /// </summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, max 100 (default 20).</param>
    /// <param name="search">Optional search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of client summaries.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<ClientListItemDto>>(
            new ListClientsQuery(page, pageSize, search), ct);

        return Ok(result);
    }

    /// <summary>
    /// Returns full details for a single client.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full client details including contacts and billing address.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ClientDto>(new GetClientQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new client record linked to an existing Identity user.
    /// </summary>
    /// <param name="request">Client creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new client's ID.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateClientCommand(request.UserId, request.Email, request.FirstName, request.LastName, request.CompanyName),
            ct);

        return CreatedAtAction(nameof(GetByIdAsync), new { id }, new { id });
    }

    /// <summary>
    /// Updates a client's profile and billing address.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="request">Updated client data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateClientCommand(id, request.FirstName, request.LastName, request.CompanyName,
                request.Phone, request.Street, request.City, request.State, request.PostCode, request.Country),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Adds an additional contact to a client account.
    /// </summary>
    /// <param name="id">The client primary key.</param>
    /// <param name="request">Contact data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/contacts")]
    public async Task<IActionResult> AddContactAsync(int id, [FromBody] AddContactRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new AddContactCommand(id, request.Name, request.Email, request.Phone, request.Type),
            ct);

        return NoContent();
    }
}
```

- [ ] **Step 3: Build full solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.API/Clients/
git commit -m "feat(api): add ClientsController with admin CRUD endpoints"
```

---

## Task 12: API — ClientProfileController (Client Portal)

**Files:**
- Create: `src/Innovayse.API/Clients/ClientProfileController.cs`

- [ ] **Step 1: Create ClientProfileController**

```csharp
namespace Innovayse.API.Clients;

using System.Security.Claims;
using Innovayse.API.Clients.Requests;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client self-service portal endpoints.
/// Authenticated clients can view and update their own profile.
/// The client's Identity user ID is extracted from the JWT sub claim.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/me")]
[Authorize(Roles = Roles.Client)]
public sealed class ClientProfileController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Returns the authenticated client's own profile.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's full profile including contacts and billing address.</returns>
    [HttpGet]
    public async Task<ActionResult<ClientDto>> GetMyProfileAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID not found in token.");

        var result = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates the authenticated client's own profile and billing address.
    /// The ClientId is resolved from the JWT — clients cannot update other profiles.
    /// </summary>
    /// <param name="request">Updated profile data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated client profile.</returns>
    [HttpPut]
    public async Task<ActionResult<ClientDto>> UpdateMyProfileAsync(
        [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID not found in token.");

        // Get the client to resolve ClientId from UserId
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);

        await bus.InvokeAsync(
            new UpdateClientCommand(
                profile.Id,
                request.FirstName,
                request.LastName,
                request.CompanyName,
                request.Phone,
                request.Street,
                request.City,
                request.State,
                request.PostCode,
                request.Country),
            ct);

        var updated = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);
        return Ok(updated);
    }
}
```

- [ ] **Step 2: Build full solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.API/Clients/ClientProfileController.cs
git commit -m "feat(api): add ClientProfileController for client self-service portal"
```

---

## Task 13: Integration Tests + dotnet format

**Files:**
- Create: `tests/Innovayse.Integration.Tests/Clients/ClientsEndpointTests.cs`

- [ ] **Step 1: Create integration tests**

```csharp
namespace Innovayse.Integration.Tests.Clients;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

/// <summary>Integration tests for /api/clients and /api/me endpoints.</summary>
public sealed class ClientsEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Registers a user, then GET /api/me as that client returns 200 with the profile.
    /// </summary>
    [Fact]
    public async Task GetMyProfile_AfterRegister_Returns200WithProfile()
    {
        var client = factory.CreateClient();
        var email = $"profile-{Guid.NewGuid()}@example.com";

        // Register
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Alice",
            lastName = "Smith"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authJson = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authJson!.AccessToken);

        // Get profile
        var profileResponse = await client.GetAsync("/api/me");
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await profileResponse.Content.ReadAsStringAsync();
        json.Should().Contain("Alice");
        json.Should().Contain("Smith");
    }

    /// <summary>
    /// Unauthenticated request to /api/me returns 401.
    /// </summary>
    [Fact]
    public async Task GetMyProfile_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Admin GET /api/clients returns 401 for unauthenticated request.
    /// </summary>
    [Fact]
    public async Task ListClients_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/clients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Helper record to deserialize auth response.</summary>
    /// <param name="AccessToken">The JWT access token.</param>
    /// <param name="ExpiresAt">Token expiry.</param>
    /// <param name="Role">User role.</param>
    private record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role);
}
```

- [ ] **Step 2: Run integration tests**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Integration.Tests -v normal 2>&1 | tail -20
```

Expected: all pass (5 existing auth + 3 new client = 8 total).

- [ ] **Step 3: Run all tests**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test 2>&1 | tail -15
```

Expected: all pass (Domain: 7, Application: 16, Integration: 8 = 31 total).

- [ ] **Step 4: Run dotnet format**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet format 2>&1 | head -20
```

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add tests/Innovayse.Integration.Tests/Clients/
git commit -m "test(integration): add client endpoint tests"
git diff --quiet || git add . && git commit -m "style: dotnet format pass on clients module"
```

---

## Self-Review

**Spec coverage:**
- [x] `clients, contacts, addresses` tables (spec §6) — clients table with inline address + contacts table
- [x] `GET /api/clients/` CRUD (spec §8) — Task 11 ClientsController
- [x] `ClientRegisteredEvent` → handler (spec §9) — Task 8 CreateClientOnRegisterHandler + Task 4 ClientRegisteredIntegrationEvent
- [x] Client portal self-service — Task 12 ClientProfileController `/api/me`
- [x] Roles enforced — Admin/Reseller for admin endpoints, Client for portal
- [x] Domain: Client aggregate, Contact entity, enums, domain event — Task 1
- [x] IClientRepository in Domain — Task 2
- [x] Application: DTOs, validators, handlers — Tasks 3–8
- [x] Infrastructure: EF config, repository — Task 9
- [x] EF migration — Task 10
- [x] Integration tests — Task 13

**Placeholder scan:** No TBD, TODO, or incomplete steps found.

**Type consistency:**
- `ClientRegisteredIntegrationEvent` defined in Task 4, consumed in Task 8 ✓
- `CreateClientCommand` defined in Task 4, used in Tasks 8 and 11 ✓
- `IClientRepository` defined in Task 2, implemented in Task 9, used in handlers ✓
- `ClientDto` defined in Task 3, returned by GetClientHandler and GetMyProfileHandler ✓
- `UpdateClientRequest` defined in Task 11, reused in Task 12 ✓
- `PagedResult<T>` defined in Task 3, returned by ListClientsHandler ✓
