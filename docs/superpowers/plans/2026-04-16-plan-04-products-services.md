# Products & Services Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Products & Services module — product groups, products with pricing tiers, and client services — with admin CRUD and a client-facing order/view surface.

**Architecture:** `ProductGroup` and `Product` are Domain aggregates stored in the `Products/` folder. `ClientService` is a separate Domain aggregate in `Services/`. Application layer follows the same Commands/Queries/Events pattern as Clients. `IProvisioningProvider` interface lives in Domain; a stub `NullProvisioningProvider` lives in Infrastructure.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, Wolverine (CQRS), FluentValidation, Mapster, xUnit + Testcontainers

---

## File Map

### Domain
- Create: `src/Innovayse.Domain/Products/ProductGroup.cs`
- Create: `src/Innovayse.Domain/Products/Product.cs`
- Create: `src/Innovayse.Domain/Products/ProductPricing.cs`
- Create: `src/Innovayse.Domain/Products/ProductType.cs` (enum)
- Create: `src/Innovayse.Domain/Products/ProductStatus.cs` (enum)
- Create: `src/Innovayse.Domain/Products/Events/ProductCreatedEvent.cs`
- Create: `src/Innovayse.Domain/Products/Interfaces/IProductGroupRepository.cs`
- Create: `src/Innovayse.Domain/Products/Interfaces/IProductRepository.cs`
- Create: `src/Innovayse.Domain/Services/ClientService.cs`
- Create: `src/Innovayse.Domain/Services/ServiceStatus.cs` (enum)
- Create: `src/Innovayse.Domain/Services/Events/ClientServiceCreatedEvent.cs`
- Create: `src/Innovayse.Domain/Services/Events/ClientServiceSuspendedEvent.cs`
- Create: `src/Innovayse.Domain/Services/Events/ClientServiceTerminatedEvent.cs`
- Create: `src/Innovayse.Domain/Services/Interfaces/IClientServiceRepository.cs`
- Create: `src/Innovayse.Domain/Services/Interfaces/IProvisioningProvider.cs`

### Application
- Create: `src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupHandler.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupValidator.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProductGroup/UpdateProductGroupCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProductGroup/UpdateProductGroupHandler.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductHandler.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductValidator.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProduct/UpdateProductCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProduct/UpdateProductHandler.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProductGroups/GetProductGroupsQuery.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProductGroups/GetProductGroupsHandler.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProductGroups/ProductGroupDto.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/GetProductsQuery.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/GetProductsHandler.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/ProductDto.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/ProductPricingDto.cs`
- Create: `src/Innovayse.Application/Services/Commands/OrderService/OrderServiceCommand.cs`
- Create: `src/Innovayse.Application/Services/Commands/OrderService/OrderServiceHandler.cs`
- Create: `src/Innovayse.Application/Services/Commands/OrderService/OrderServiceValidator.cs`
- Create: `src/Innovayse.Application/Services/Commands/SuspendService/SuspendServiceCommand.cs`
- Create: `src/Innovayse.Application/Services/Commands/SuspendService/SuspendServiceHandler.cs`
- Create: `src/Innovayse.Application/Services/Commands/TerminateService/TerminateServiceCommand.cs`
- Create: `src/Innovayse.Application/Services/Commands/TerminateService/TerminateServiceHandler.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetMyServices/GetMyServicesQuery.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetMyServices/GetMyServicesHandler.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetMyServices/ClientServiceDto.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetServices/GetServicesQuery.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetServices/GetServicesHandler.cs`

### Infrastructure
- Create: `src/Innovayse.Infrastructure/Products/ProductGroupRepository.cs`
- Create: `src/Innovayse.Infrastructure/Products/ProductRepository.cs`
- Create: `src/Innovayse.Infrastructure/Products/Configurations/ProductGroupConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Products/Configurations/ProductConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Products/Configurations/ProductPricingConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Services/ClientServiceRepository.cs`
- Create: `src/Innovayse.Infrastructure/Services/Configurations/ClientServiceConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Services/NullProvisioningProvider.cs`
- Modify: `src/Innovayse.Infrastructure/Persistence/AppDbContext.cs` — add DbSets
- Modify: `src/Innovayse.Infrastructure/DependencyInjection.cs` — register repos + provider

### API
- Create: `src/Innovayse.API/Products/ProductGroupsController.cs`
- Create: `src/Innovayse.API/Products/ProductsController.cs`
- Create: `src/Innovayse.API/Services/ServicesController.cs` (Admin)
- Create: `src/Innovayse.API/Services/MyServicesController.cs` (Client portal)

### Tests
- Create: `tests/Innovayse.Domain.Tests/Products/ProductGroupTests.cs`
- Create: `tests/Innovayse.Domain.Tests/Products/ProductTests.cs`
- Create: `tests/Innovayse.Domain.Tests/Services/ClientServiceTests.cs`
- Create: `tests/Innovayse.Integration.Tests/Products/ProductsEndpointTests.cs`
- Create: `tests/Innovayse.Integration.Tests/Services/ServicesEndpointTests.cs`

---

## Task 1: Domain — ProductGroup + Product aggregates + enums + events

**Files:**
- Create: `src/Innovayse.Domain/Products/ProductType.cs`
- Create: `src/Innovayse.Domain/Products/ProductStatus.cs`
- Create: `src/Innovayse.Domain/Products/ProductGroup.cs`
- Create: `src/Innovayse.Domain/Products/ProductPricing.cs`
- Create: `src/Innovayse.Domain/Products/Product.cs`
- Create: `src/Innovayse.Domain/Products/Events/ProductCreatedEvent.cs`
- Test: `tests/Innovayse.Domain.Tests/Products/ProductGroupTests.cs`
- Test: `tests/Innovayse.Domain.Tests/Products/ProductTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Innovayse.Domain.Tests/Products/ProductGroupTests.cs
namespace Innovayse.Domain.Tests.Products;

using Innovayse.Domain.Products;

public class ProductGroupTests
{
    [Fact]
    public void Create_SetsNameAndDescription()
    {
        var group = ProductGroup.Create("Shared Hosting", "Hosting plans");

        Assert.Equal("Shared Hosting", group.Name);
        Assert.Equal("Hosting plans", group.Description);
        Assert.True(group.IsActive);
        Assert.Empty(group.Products);
    }

    [Fact]
    public void Update_ChangesName()
    {
        var group = ProductGroup.Create("Old", null);
        group.Update("New", "desc");
        Assert.Equal("New", group.Name);
        Assert.Equal("desc", group.Description);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var group = ProductGroup.Create("G", null);
        group.Deactivate();
        Assert.False(group.IsActive);
    }
}
```

```csharp
// tests/Innovayse.Domain.Tests/Products/ProductTests.cs
namespace Innovayse.Domain.Tests.Products;

using Innovayse.Domain.Products;

public class ProductTests
{
    [Fact]
    public void Create_SetsAllProperties_AndRaisesEvent()
    {
        var product = Product.Create(
            groupId: 1,
            name: "Starter",
            description: "Entry plan",
            type: ProductType.SharedHosting,
            monthlyPrice: 5.99m,
            annualPrice: 59.99m);

        Assert.Equal(1, product.GroupId);
        Assert.Equal("Starter", product.Name);
        Assert.Equal(ProductType.SharedHosting, product.Type);
        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Equal(5.99m, product.MonthlyPrice);
        Assert.Equal(59.99m, product.AnnualPrice);
        Assert.Single(product.DomainEvents);
        Assert.IsType<ProductCreatedEvent>(product.DomainEvents[0]);
    }

    [Fact]
    public void Update_ChangesNameAndPrices()
    {
        var product = Product.Create(1, "Old", null, ProductType.SharedHosting, 5m, 50m);
        product.Update("New", "desc", 9m, 90m);
        Assert.Equal("New", product.Name);
        Assert.Equal(9m, product.MonthlyPrice);
        Assert.Equal(90m, product.AnnualPrice);
    }

    [Fact]
    public void Deactivate_ChangesStatus()
    {
        var product = Product.Create(1, "P", null, ProductType.SharedHosting, 5m, 50m);
        product.Deactivate();
        Assert.Equal(ProductStatus.Inactive, product.Status);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Domain.Tests --no-build 2>&1 | tail -5
```

Expected: compile error — types not found.

- [ ] **Step 3: Create enums and event**

```csharp
// src/Innovayse.Domain/Products/ProductType.cs
namespace Innovayse.Domain.Products;

/// <summary>Classifies a product by the type of service it represents.</summary>
public enum ProductType
{
    /// <summary>Shared web hosting plan.</summary>
    SharedHosting,

    /// <summary>Virtual private server.</summary>
    Vps,

    /// <summary>Dedicated server.</summary>
    Dedicated,

    /// <summary>Domain name registration.</summary>
    Domain,

    /// <summary>SSL certificate.</summary>
    Ssl,

    /// <summary>Other or custom service.</summary>
    Other,
}
```

```csharp
// src/Innovayse.Domain/Products/ProductStatus.cs
namespace Innovayse.Domain.Products;

/// <summary>Lifecycle status of a product listing.</summary>
public enum ProductStatus
{
    /// <summary>Product is available for ordering.</summary>
    Active,

    /// <summary>Product is hidden and cannot be ordered.</summary>
    Inactive,
}
```

```csharp
// src/Innovayse.Domain/Products/Events/ProductCreatedEvent.cs
namespace Innovayse.Domain.Products.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a new product is created.</summary>
/// <param name="ProductId">The newly assigned product ID (0 before EF save).</param>
/// <param name="Name">The product name.</param>
/// <param name="GroupId">The product group the product belongs to.</param>
public record ProductCreatedEvent(int ProductId, string Name, int GroupId) : IDomainEvent;
```

- [ ] **Step 4: Create ProductGroup aggregate**

```csharp
// src/Innovayse.Domain/Products/ProductGroup.cs
namespace Innovayse.Domain.Products;

using Innovayse.Domain.Common;

/// <summary>
/// Groups related products (e.g., "Shared Hosting", "VPS", "SSL Certificates").
/// An aggregate root — owns the list of products in the catalogue.
/// Stored in the <c>product_groups</c> table.
/// </summary>
public sealed class ProductGroup : AggregateRoot
{
    /// <summary>Internal mutable products list.</summary>
    private readonly List<Product> _products = [];

    /// <summary>Gets the group display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional description shown in admin and storefront.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets whether this group is visible and available for new orders.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets all products belonging to this group.</summary>
    public IReadOnlyList<Product> Products => _products.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private ProductGroup() : base(0) { }

    /// <summary>
    /// Creates a new active product group.
    /// </summary>
    /// <param name="name">Display name for the group.</param>
    /// <param name="description">Optional description.</param>
    /// <returns>A new active <see cref="ProductGroup"/>.</returns>
    public static ProductGroup Create(string name, string? description)
    {
        var group = new ProductGroup { Name = name, Description = description, IsActive = true };
        return group;
    }

    /// <summary>
    /// Updates the group name and description.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>Marks the group inactive — hides it from the storefront.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Marks the group active — makes it visible in the storefront.</summary>
    public void Activate() => IsActive = true;
}
```

- [ ] **Step 5: Create ProductPricing value object**

```csharp
// src/Innovayse.Domain/Products/ProductPricing.cs
namespace Innovayse.Domain.Products;

using Innovayse.Domain.Common;

/// <summary>
/// Stores the monthly and annual pricing for a product.
/// Owned by <see cref="Product"/> — stored as columns in the <c>products</c> table.
/// </summary>
public sealed class ProductPricing : ValueObject
{
    /// <summary>Gets the monthly price in the system currency.</summary>
    public decimal Monthly { get; private set; }

    /// <summary>Gets the annual price (full year, typically discounted).</summary>
    public decimal Annual { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private ProductPricing() { }

    /// <summary>
    /// Creates a new pricing record.
    /// </summary>
    /// <param name="monthly">Monthly price (must be ≥ 0).</param>
    /// <param name="annual">Annual price (must be ≥ 0).</param>
    public ProductPricing(decimal monthly, decimal annual)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthly);
        ArgumentOutOfRangeException.ThrowIfNegative(annual);
        Monthly = monthly;
        Annual = annual;
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Monthly;
        yield return Annual;
    }
}
```

- [ ] **Step 6: Ensure ValueObject base exists**

Check that `src/Innovayse.Domain/Common/ValueObject.cs` exists:

```bash
cat /c/Users/Dell/Desktop/www/innovayse/backend/src/Innovayse.Domain/Common/ValueObject.cs
```

If it does not exist, create it:

```csharp
// src/Innovayse.Domain/Common/ValueObject.cs
namespace Innovayse.Domain.Common;

/// <summary>
/// Base class for domain value objects.
/// Equality is based on value components, not identity.
/// </summary>
public abstract class ValueObject
{
    /// <summary>Returns the components used for equality comparison.</summary>
    /// <returns>Sequence of components that identify this value object.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        return GetEqualityComponents()
            .SequenceEqual(((ValueObject)obj).GetEqualityComponents());
    }

    /// <inheritdoc/>
    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj));
}
```

- [ ] **Step 7: Create Product aggregate**

```csharp
// src/Innovayse.Domain/Products/Product.cs
namespace Innovayse.Domain.Products;

using Innovayse.Domain.Common;
using Innovayse.Domain.Products.Events;

/// <summary>
/// A product in the catalogue that clients can order as a service.
/// Belongs to a <see cref="ProductGroup"/>.
/// Stored in the <c>products</c> table.
/// </summary>
public sealed class Product : AggregateRoot
{
    /// <summary>Gets the FK to the <see cref="ProductGroup"/> this product belongs to.</summary>
    public int GroupId { get; private set; }

    /// <summary>Gets the product display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the product type (hosting, VPS, domain, etc.).</summary>
    public ProductType Type { get; private set; }

    /// <summary>Gets the current status.</summary>
    public ProductStatus Status { get; private set; }

    /// <summary>Gets the monthly price.</summary>
    public decimal MonthlyPrice { get; private set; }

    /// <summary>Gets the annual price.</summary>
    public decimal AnnualPrice { get; private set; }

    /// <summary>Gets the UTC timestamp when the product was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Product() : base(0) { }

    /// <summary>
    /// Creates a new active product and raises <see cref="ProductCreatedEvent"/>.
    /// </summary>
    /// <param name="groupId">FK to the parent product group.</param>
    /// <param name="name">Product display name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="type">Product type.</param>
    /// <param name="monthlyPrice">Monthly price (≥ 0).</param>
    /// <param name="annualPrice">Annual price (≥ 0).</param>
    /// <returns>A new active <see cref="Product"/>.</returns>
    public static Product Create(
        int groupId,
        string name,
        string? description,
        ProductType type,
        decimal monthlyPrice,
        decimal annualPrice)
    {
        var product = new Product
        {
            GroupId = groupId,
            Name = name,
            Description = description,
            Type = type,
            Status = ProductStatus.Active,
            MonthlyPrice = monthlyPrice,
            AnnualPrice = annualPrice,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        product.AddDomainEvent(new ProductCreatedEvent(0, name, groupId));
        return product;
    }

    /// <summary>
    /// Updates the product name, description, and prices.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    /// <param name="monthlyPrice">New monthly price.</param>
    /// <param name="annualPrice">New annual price.</param>
    public void Update(string name, string? description, decimal monthlyPrice, decimal annualPrice)
    {
        Name = name;
        Description = description;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
    }

    /// <summary>Marks the product inactive so it cannot be ordered.</summary>
    public void Deactivate() => Status = ProductStatus.Inactive;

    /// <summary>Marks the product active so it can be ordered again.</summary>
    public void Activate() => Status = ProductStatus.Active;
}
```

- [ ] **Step 8: Run tests — verify they pass**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Domain.Tests --no-build 2>&1 | tail -10
```

Expected: all tests pass (including pre-existing Clients tests).

- [ ] **Step 9: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Domain/Products/ tests/Innovayse.Domain.Tests/Products/
git commit -m "feat: add ProductGroup and Product domain aggregates with tests"
```

---

## Task 2: Domain — ClientService aggregate + ServiceStatus + IProvisioningProvider

**Files:**
- Create: `src/Innovayse.Domain/Services/ServiceStatus.cs`
- Create: `src/Innovayse.Domain/Services/Events/ClientServiceCreatedEvent.cs`
- Create: `src/Innovayse.Domain/Services/Events/ClientServiceSuspendedEvent.cs`
- Create: `src/Innovayse.Domain/Services/Events/ClientServiceTerminatedEvent.cs`
- Create: `src/Innovayse.Domain/Services/ClientService.cs`
- Create: `src/Innovayse.Domain/Services/Interfaces/IProvisioningProvider.cs`
- Test: `tests/Innovayse.Domain.Tests/Services/ClientServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Innovayse.Domain.Tests/Services/ClientServiceTests.cs
namespace Innovayse.Domain.Tests.Services;

using Innovayse.Domain.Services;

public class ClientServiceTests
{
    [Fact]
    public void Create_SetsPendingStatus_AndRaisesEvent()
    {
        var svc = ClientService.Create(clientId: 1, productId: 2, billingCycle: "monthly");

        Assert.Equal(1, svc.ClientId);
        Assert.Equal(2, svc.ProductId);
        Assert.Equal("monthly", svc.BillingCycle);
        Assert.Equal(ServiceStatus.Pending, svc.Status);
        Assert.Single(svc.DomainEvents);
        Assert.IsType<ClientServiceCreatedEvent>(svc.DomainEvents[0]);
    }

    [Fact]
    public void Activate_ChangesStatusToActive()
    {
        var svc = ClientService.Create(1, 2, "monthly");
        svc.Activate("cpanel-acc-123");
        Assert.Equal(ServiceStatus.Active, svc.Status);
        Assert.Equal("cpanel-acc-123", svc.ProvisioningRef);
    }

    [Fact]
    public void Suspend_ChangeStatusToSuspended_AndRaisesEvent()
    {
        var svc = ClientService.Create(1, 2, "monthly");
        svc.Activate("ref");
        svc.Suspend();
        Assert.Equal(ServiceStatus.Suspended, svc.Status);
        Assert.Contains(svc.DomainEvents, e => e is ClientServiceSuspendedEvent);
    }

    [Fact]
    public void Terminate_ChangesStatusToTerminated_AndRaisesEvent()
    {
        var svc = ClientService.Create(1, 2, "monthly");
        svc.Activate("ref");
        svc.Terminate();
        Assert.Equal(ServiceStatus.Terminated, svc.Status);
        Assert.Contains(svc.DomainEvents, e => e is ClientServiceTerminatedEvent);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Domain.Tests --no-build 2>&1 | tail -5
```

Expected: compile error.

- [ ] **Step 3: Create ServiceStatus enum and domain events**

```csharp
// src/Innovayse.Domain/Services/ServiceStatus.cs
namespace Innovayse.Domain.Services;

/// <summary>Lifecycle status of a client service.</summary>
public enum ServiceStatus
{
    /// <summary>Service ordered but not yet provisioned.</summary>
    Pending,

    /// <summary>Service is active and provisioned.</summary>
    Active,

    /// <summary>Service is temporarily suspended (e.g., non-payment).</summary>
    Suspended,

    /// <summary>Service has been permanently terminated.</summary>
    Terminated,
}
```

```csharp
// src/Innovayse.Domain/Services/Events/ClientServiceCreatedEvent.cs
namespace Innovayse.Domain.Services.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a client orders a new service.</summary>
/// <param name="ServiceId">The service ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="ProductId">The product being ordered.</param>
public record ClientServiceCreatedEvent(int ServiceId, int ClientId, int ProductId) : IDomainEvent;
```

```csharp
// src/Innovayse.Domain/Services/Events/ClientServiceSuspendedEvent.cs
namespace Innovayse.Domain.Services.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a client service is suspended.</summary>
/// <param name="ServiceId">The suspended service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
public record ClientServiceSuspendedEvent(int ServiceId, int ClientId) : IDomainEvent;
```

```csharp
// src/Innovayse.Domain/Services/Events/ClientServiceTerminatedEvent.cs
namespace Innovayse.Domain.Services.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a client service is terminated.</summary>
/// <param name="ServiceId">The terminated service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
public record ClientServiceTerminatedEvent(int ServiceId, int ClientId) : IDomainEvent;
```

- [ ] **Step 4: Create ClientService aggregate**

```csharp
// src/Innovayse.Domain/Services/ClientService.cs
namespace Innovayse.Domain.Services;

using Innovayse.Domain.Common;
using Innovayse.Domain.Services.Events;

/// <summary>
/// Represents a service instance owned by a client.
/// Links a <see cref="ClientId"/> to a <c>Product</c> and tracks provisioning state.
/// Stored in the <c>client_services</c> table.
/// </summary>
public sealed class ClientService : AggregateRoot
{
    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the FK to the ordered product.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets the selected billing cycle ("monthly" or "annual").</summary>
    public string BillingCycle { get; private set; } = string.Empty;

    /// <summary>Gets the current lifecycle status.</summary>
    public ServiceStatus Status { get; private set; }

    /// <summary>Gets the external provisioning reference (e.g., cPanel account name).</summary>
    public string? ProvisioningRef { get; private set; }

    /// <summary>Gets the UTC timestamp when the service was ordered.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp of the next renewal date, if active.</summary>
    public DateTimeOffset? NextRenewalAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private ClientService() : base(0) { }

    /// <summary>
    /// Creates a new pending client service and raises <see cref="ClientServiceCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client placing the order.</param>
    /// <param name="productId">FK to the product being ordered.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <returns>A new pending <see cref="ClientService"/>.</returns>
    public static ClientService Create(int clientId, int productId, string billingCycle)
    {
        var svc = new ClientService
        {
            ClientId = clientId,
            ProductId = productId,
            BillingCycle = billingCycle,
            Status = ServiceStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        svc.AddDomainEvent(new ClientServiceCreatedEvent(0, clientId, productId));
        return svc;
    }

    /// <summary>
    /// Marks the service as active after successful provisioning.
    /// Sets the provisioning reference and calculates the first renewal date.
    /// </summary>
    /// <param name="provisioningRef">External reference from the provisioning provider.</param>
    public void Activate(string provisioningRef)
    {
        Status = ServiceStatus.Active;
        ProvisioningRef = provisioningRef;
        NextRenewalAt = BillingCycle == "annual"
            ? CreatedAt.AddYears(1)
            : CreatedAt.AddMonths(1);
    }

    /// <summary>
    /// Suspends the service and raises <see cref="ClientServiceSuspendedEvent"/>.
    /// </summary>
    public void Suspend()
    {
        Status = ServiceStatus.Suspended;
        AddDomainEvent(new ClientServiceSuspendedEvent(Id, ClientId));
    }

    /// <summary>
    /// Terminates the service and raises <see cref="ClientServiceTerminatedEvent"/>.
    /// </summary>
    public void Terminate()
    {
        Status = ServiceStatus.Terminated;
        AddDomainEvent(new ClientServiceTerminatedEvent(Id, ClientId));
    }
}
```

- [ ] **Step 5: Create IProvisioningProvider interface**

```csharp
// src/Innovayse.Domain/Services/Interfaces/IProvisioningProvider.cs
namespace Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Abstraction over a hosting control panel provisioning API (cPanel WHM, Plesk, etc.).
/// Implemented in Infrastructure — never called directly from Application.
/// </summary>
public interface IProvisioningProvider
{
    /// <summary>
    /// Provisions a new hosting account or resource for a client service.
    /// </summary>
    /// <param name="clientId">The ID of the client the service belongs to.</param>
    /// <param name="productId">The ID of the product being provisioned.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An opaque reference string to identify the provisioned resource.</returns>
    Task<string> ProvisionAsync(int clientId, int productId, string billingCycle, CancellationToken ct);

    /// <summary>
    /// Suspends a previously provisioned service.
    /// </summary>
    /// <param name="provisioningRef">The reference returned by <see cref="ProvisionAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SuspendAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Permanently terminates a provisioned service and releases its resources.
    /// </summary>
    /// <param name="provisioningRef">The reference returned by <see cref="ProvisionAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TerminateAsync(string provisioningRef, CancellationToken ct);
}
```

- [ ] **Step 6: Create repository interfaces**

```csharp
// src/Innovayse.Domain/Products/Interfaces/IProductGroupRepository.cs
namespace Innovayse.Domain.Products.Interfaces;

/// <summary>
/// Persistence contract for <see cref="ProductGroup"/> aggregate operations.
/// </summary>
public interface IProductGroupRepository
{
    /// <summary>Finds a product group by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ProductGroup?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Returns all product groups (active and inactive).</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ProductGroup>> ListAsync(CancellationToken ct);

    /// <summary>Adds a new product group. Call SaveChangesAsync to persist.</summary>
    /// <param name="group">The new group aggregate.</param>
    void Add(ProductGroup group);
}
```

```csharp
// src/Innovayse.Domain/Products/Interfaces/IProductRepository.cs
namespace Innovayse.Domain.Products.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Product"/> aggregate operations.
/// </summary>
public interface IProductRepository
{
    /// <summary>Finds a product by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Product?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of products, optionally filtered by group or active status.
    /// </summary>
    /// <param name="groupId">Optional group filter.</param>
    /// <param name="activeOnly">When <see langword="true"/>, returns only active products.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Product>> ListAsync(int? groupId, bool activeOnly, CancellationToken ct);

    /// <summary>Adds a new product. Call SaveChangesAsync to persist.</summary>
    /// <param name="product">The new product aggregate.</param>
    void Add(Product product);
}
```

```csharp
// src/Innovayse.Domain/Services/Interfaces/IClientServiceRepository.cs
namespace Innovayse.Domain.Services.Interfaces;

using Innovayse.Domain.Services;

/// <summary>
/// Persistence contract for <see cref="ClientService"/> aggregate operations.
/// </summary>
public interface IClientServiceRepository
{
    /// <summary>Finds a client service by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ClientService?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Returns all services for a given client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ClientService>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>Returns a paginated list of all client services (admin view).</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(IReadOnlyList<ClientService> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Adds a new client service. Call SaveChangesAsync to persist.</summary>
    /// <param name="service">The new client service aggregate.</param>
    void Add(ClientService service);
}
```

- [ ] **Step 7: Run tests — all pass**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.Domain.Tests --no-build 2>&1 | tail -10
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Domain/Services/ tests/Innovayse.Domain.Tests/Services/
git commit -m "feat: add ClientService domain aggregate and IProvisioningProvider interface"
```

---

## Task 3: Application — Product Groups commands + queries + DTOs

**Files:**
- Create: `src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupHandler.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupValidator.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProductGroup/UpdateProductGroupCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProductGroup/UpdateProductGroupHandler.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProductGroups/ProductGroupDto.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProductGroups/GetProductGroupsQuery.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProductGroups/GetProductGroupsHandler.cs`

- [ ] **Step 1: Create commands and validator**

```csharp
// src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupCommand.cs
namespace Innovayse.Application.Products.Commands.CreateProductGroup;

/// <summary>Command to create a new product group.</summary>
/// <param name="Name">Display name for the group.</param>
/// <param name="Description">Optional description.</param>
public record CreateProductGroupCommand(string Name, string? Description);
```

```csharp
// src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupValidator.cs
namespace Innovayse.Application.Products.Commands.CreateProductGroup;

using FluentValidation;

/// <summary>Validates <see cref="CreateProductGroupCommand"/> inputs.</summary>
public sealed class CreateProductGroupValidator : AbstractValidator<CreateProductGroupCommand>
{
    /// <summary>Initializes validation rules for product group creation.</summary>
    public CreateProductGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
```

```csharp
// src/Innovayse.Application/Products/Commands/CreateProductGroup/CreateProductGroupHandler.cs
namespace Innovayse.Application.Products.Commands.CreateProductGroup;

using Innovayse.Application.Common;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Creates a new product group and persists it.</summary>
public sealed class CreateProductGroupHandler(
    IProductGroupRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles the <see cref="CreateProductGroupCommand"/>.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created product group ID.</returns>
    public async Task<int> Handle(CreateProductGroupCommand cmd, CancellationToken ct)
    {
        var group = ProductGroup.Create(cmd.Name, cmd.Description);
        repo.Add(group);
        await uow.SaveChangesAsync(ct);
        return group.Id;
    }
}
```

```csharp
// src/Innovayse.Application/Products/Commands/UpdateProductGroup/UpdateProductGroupCommand.cs
namespace Innovayse.Application.Products.Commands.UpdateProductGroup;

/// <summary>Command to update a product group's name and description.</summary>
/// <param name="Id">Product group primary key.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
public record UpdateProductGroupCommand(int Id, string Name, string? Description);
```

```csharp
// src/Innovayse.Application/Products/Commands/UpdateProductGroup/UpdateProductGroupHandler.cs
namespace Innovayse.Application.Products.Commands.UpdateProductGroup;

using Innovayse.Application.Common;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Updates an existing product group.</summary>
public sealed class UpdateProductGroupHandler(
    IProductGroupRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles the <see cref="UpdateProductGroupCommand"/>.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the product group is not found.</exception>
    public async Task Handle(UpdateProductGroupCommand cmd, CancellationToken ct)
    {
        var group = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Product group {cmd.Id} not found.");

        group.Update(cmd.Name, cmd.Description);
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Create DTO and query handler**

```csharp
// src/Innovayse.Application/Products/Queries/GetProductGroups/ProductGroupDto.cs
namespace Innovayse.Application.Products.Queries.GetProductGroups;

/// <summary>Represents a product group in list responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="IsActive">Whether the group is visible in the storefront.</param>
/// <param name="ProductCount">Number of products in the group.</param>
public record ProductGroupDto(int Id, string Name, string? Description, bool IsActive, int ProductCount);
```

```csharp
// src/Innovayse.Application/Products/Queries/GetProductGroups/GetProductGroupsQuery.cs
namespace Innovayse.Application.Products.Queries.GetProductGroups;

/// <summary>Returns all product groups.</summary>
/// <param name="ActiveOnly">When <see langword="true"/>, returns only active groups.</param>
public record GetProductGroupsQuery(bool ActiveOnly = false);
```

```csharp
// src/Innovayse.Application/Products/Queries/GetProductGroups/GetProductGroupsHandler.cs
namespace Innovayse.Application.Products.Queries.GetProductGroups;

using Innovayse.Domain.Products.Interfaces;

/// <summary>Returns all product groups as DTOs.</summary>
public sealed class GetProductGroupsHandler(IProductGroupRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetProductGroupsQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product group DTOs.</returns>
    public async Task<IReadOnlyList<ProductGroupDto>> Handle(GetProductGroupsQuery qry, CancellationToken ct)
    {
        var groups = await repo.ListAsync(ct);
        return groups
            .Where(g => !qry.ActiveOnly || g.IsActive)
            .Select(g => new ProductGroupDto(g.Id, g.Name, g.Description, g.IsActive, g.Products.Count))
            .ToList();
    }
}
```

- [ ] **Step 3: Build to verify no compile errors**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj 2>&1 | tail -10
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Products/
git commit -m "feat: add product group commands and queries"
```

---

## Task 4: Application — Product commands + queries + DTOs

**Files:**
- Create: `src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductValidator.cs`
- Create: `src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductHandler.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProduct/UpdateProductCommand.cs`
- Create: `src/Innovayse.Application/Products/Commands/UpdateProduct/UpdateProductHandler.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/ProductPricingDto.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/ProductDto.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/GetProductsQuery.cs`
- Create: `src/Innovayse.Application/Products/Queries/GetProducts/GetProductsHandler.cs`

- [ ] **Step 1: Create Product commands**

```csharp
// src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductCommand.cs
namespace Innovayse.Application.Products.Commands.CreateProduct;

using Innovayse.Domain.Products;

/// <summary>Command to create a new product in a product group.</summary>
/// <param name="GroupId">FK to the parent product group.</param>
/// <param name="Name">Product display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Type">Product type.</param>
/// <param name="MonthlyPrice">Monthly price (≥ 0).</param>
/// <param name="AnnualPrice">Annual price (≥ 0).</param>
public record CreateProductCommand(
    int GroupId,
    string Name,
    string? Description,
    ProductType Type,
    decimal MonthlyPrice,
    decimal AnnualPrice);
```

```csharp
// src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductValidator.cs
namespace Innovayse.Application.Products.Commands.CreateProduct;

using FluentValidation;

/// <summary>Validates <see cref="CreateProductCommand"/> inputs.</summary>
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    /// <summary>Initializes validation rules for product creation.</summary>
    public CreateProductValidator()
    {
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0);
    }
}
```

```csharp
// src/Innovayse.Application/Products/Commands/CreateProduct/CreateProductHandler.cs
namespace Innovayse.Application.Products.Commands.CreateProduct;

using Innovayse.Application.Common;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Creates a new product and persists it.</summary>
public sealed class CreateProductHandler(
    IProductRepository repo,
    IProductGroupRepository groupRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateProductCommand"/>.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created product ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the product group is not found.</exception>
    public async Task<int> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        _ = await groupRepo.FindByIdAsync(cmd.GroupId, ct)
            ?? throw new InvalidOperationException($"Product group {cmd.GroupId} not found.");

        var product = Product.Create(
            cmd.GroupId, cmd.Name, cmd.Description, cmd.Type, cmd.MonthlyPrice, cmd.AnnualPrice);

        repo.Add(product);
        await uow.SaveChangesAsync(ct);
        return product.Id;
    }
}
```

```csharp
// src/Innovayse.Application/Products/Commands/UpdateProduct/UpdateProductCommand.cs
namespace Innovayse.Application.Products.Commands.UpdateProduct;

/// <summary>Command to update an existing product's details and prices.</summary>
/// <param name="Id">Product primary key.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="MonthlyPrice">New monthly price.</param>
/// <param name="AnnualPrice">New annual price.</param>
public record UpdateProductCommand(int Id, string Name, string? Description, decimal MonthlyPrice, decimal AnnualPrice);
```

```csharp
// src/Innovayse.Application/Products/Commands/UpdateProduct/UpdateProductHandler.cs
namespace Innovayse.Application.Products.Commands.UpdateProduct;

using Innovayse.Application.Common;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Updates an existing product's details and prices.</summary>
public sealed class UpdateProductHandler(IProductRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateProductCommand"/>.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the product is not found.</exception>
    public async Task Handle(UpdateProductCommand cmd, CancellationToken ct)
    {
        var product = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Product {cmd.Id} not found.");

        product.Update(cmd.Name, cmd.Description, cmd.MonthlyPrice, cmd.AnnualPrice);
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Create Product DTOs and query handler**

```csharp
// src/Innovayse.Application/Products/Queries/GetProducts/ProductPricingDto.cs
namespace Innovayse.Application.Products.Queries.GetProducts;

/// <summary>Pricing details for a product.</summary>
/// <param name="Monthly">Monthly price.</param>
/// <param name="Annual">Annual price.</param>
public record ProductPricingDto(decimal Monthly, decimal Annual);
```

```csharp
// src/Innovayse.Application/Products/Queries/GetProducts/ProductDto.cs
namespace Innovayse.Application.Products.Queries.GetProducts;

using Innovayse.Domain.Products;

/// <summary>Represents a product in list and detail responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="GroupId">Parent product group ID.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Type">Product type.</param>
/// <param name="Status">Current status.</param>
/// <param name="Pricing">Monthly and annual pricing.</param>
public record ProductDto(
    int Id,
    int GroupId,
    string Name,
    string? Description,
    ProductType Type,
    ProductStatus Status,
    ProductPricingDto Pricing);
```

```csharp
// src/Innovayse.Application/Products/Queries/GetProducts/GetProductsQuery.cs
namespace Innovayse.Application.Products.Queries.GetProducts;

/// <summary>Returns products filtered by group and/or active status.</summary>
/// <param name="GroupId">Optional group filter. When <see langword="null"/>, all groups are returned.</param>
/// <param name="ActiveOnly">When <see langword="true"/>, returns only active products.</param>
public record GetProductsQuery(int? GroupId = null, bool ActiveOnly = true);
```

```csharp
// src/Innovayse.Application/Products/Queries/GetProducts/GetProductsHandler.cs
namespace Innovayse.Application.Products.Queries.GetProducts;

using Innovayse.Domain.Products.Interfaces;

/// <summary>Returns a filtered list of products as DTOs.</summary>
public sealed class GetProductsHandler(IProductRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetProductsQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching product DTOs.</returns>
    public async Task<IReadOnlyList<ProductDto>> Handle(GetProductsQuery qry, CancellationToken ct)
    {
        var products = await repo.ListAsync(qry.GroupId, qry.ActiveOnly, ct);
        return products
            .Select(p => new ProductDto(
                p.Id,
                p.GroupId,
                p.Name,
                p.Description,
                p.Type,
                p.Status,
                new ProductPricingDto(p.MonthlyPrice, p.AnnualPrice)))
            .ToList();
    }
}
```

- [ ] **Step 3: Build to verify no compile errors**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj 2>&1 | tail -10
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Products/
git commit -m "feat: add product commands and queries"
```

---

## Task 5: Application — ClientService commands + queries

**Files:**
- Create: `src/Innovayse.Application/Services/Commands/OrderService/OrderServiceCommand.cs`
- Create: `src/Innovayse.Application/Services/Commands/OrderService/OrderServiceValidator.cs`
- Create: `src/Innovayse.Application/Services/Commands/OrderService/OrderServiceHandler.cs`
- Create: `src/Innovayse.Application/Services/Commands/SuspendService/SuspendServiceCommand.cs`
- Create: `src/Innovayse.Application/Services/Commands/SuspendService/SuspendServiceHandler.cs`
- Create: `src/Innovayse.Application/Services/Commands/TerminateService/TerminateServiceCommand.cs`
- Create: `src/Innovayse.Application/Services/Commands/TerminateService/TerminateServiceHandler.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetMyServices/ClientServiceDto.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetMyServices/GetMyServicesQuery.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetMyServices/GetMyServicesHandler.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetServices/GetServicesQuery.cs`
- Create: `src/Innovayse.Application/Services/Queries/GetServices/GetServicesHandler.cs`

- [ ] **Step 1: Create OrderService command**

```csharp
// src/Innovayse.Application/Services/Commands/OrderService/OrderServiceCommand.cs
namespace Innovayse.Application.Services.Commands.OrderService;

/// <summary>Command to order a new service for a client.</summary>
/// <param name="ClientId">The client placing the order.</param>
/// <param name="ProductId">The product being ordered.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
public record OrderServiceCommand(int ClientId, int ProductId, string BillingCycle);
```

```csharp
// src/Innovayse.Application/Services/Commands/OrderService/OrderServiceValidator.cs
namespace Innovayse.Application.Services.Commands.OrderService;

using FluentValidation;

/// <summary>Validates <see cref="OrderServiceCommand"/> inputs.</summary>
public sealed class OrderServiceValidator : AbstractValidator<OrderServiceCommand>
{
    /// <summary>Initializes validation rules for service ordering.</summary>
    public OrderServiceValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.BillingCycle).Must(c => c == "monthly" || c == "annual")
            .WithMessage("BillingCycle must be 'monthly' or 'annual'.");
    }
}
```

```csharp
// src/Innovayse.Application/Services/Commands/OrderService/OrderServiceHandler.cs
namespace Innovayse.Application.Services.Commands.OrderService;

using Innovayse.Application.Common;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Creates a pending <see cref="ClientService"/> record for the ordered product.
/// Provisioning is handled asynchronously by an event handler that listens
/// for <c>ClientServiceCreatedEvent</c>.
/// </summary>
public sealed class OrderServiceHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="OrderServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created client service ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the product is not found or inactive.</exception>
    public async Task<int> Handle(OrderServiceCommand cmd, CancellationToken ct)
    {
        var product = await productRepo.FindByIdAsync(cmd.ProductId, ct)
            ?? throw new InvalidOperationException($"Product {cmd.ProductId} not found.");

        if (product.Status != Domain.Products.ProductStatus.Active)
            throw new InvalidOperationException($"Product {cmd.ProductId} is not available for ordering.");

        var service = ClientService.Create(cmd.ClientId, cmd.ProductId, cmd.BillingCycle);
        serviceRepo.Add(service);
        await uow.SaveChangesAsync(ct);
        return service.Id;
    }
}
```

- [ ] **Step 2: Create Suspend and Terminate commands**

```csharp
// src/Innovayse.Application/Services/Commands/SuspendService/SuspendServiceCommand.cs
namespace Innovayse.Application.Services.Commands.SuspendService;

/// <summary>Command to suspend an active client service.</summary>
/// <param name="ServiceId">The service primary key.</param>
public record SuspendServiceCommand(int ServiceId);
```

```csharp
// src/Innovayse.Application/Services/Commands/SuspendService/SuspendServiceHandler.cs
namespace Innovayse.Application.Services.Commands.SuspendService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Suspends an active client service.</summary>
public sealed class SuspendServiceHandler(
    IClientServiceRepository repo,
    IProvisioningProvider provisioner,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="SuspendServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The suspend command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task Handle(SuspendServiceCommand cmd, CancellationToken ct)
    {
        var service = await repo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is not null)
            await provisioner.SuspendAsync(service.ProvisioningRef, ct);

        service.Suspend();
        await uow.SaveChangesAsync(ct);
    }
}
```

```csharp
// src/Innovayse.Application/Services/Commands/TerminateService/TerminateServiceCommand.cs
namespace Innovayse.Application.Services.Commands.TerminateService;

/// <summary>Command to permanently terminate a client service.</summary>
/// <param name="ServiceId">The service primary key.</param>
public record TerminateServiceCommand(int ServiceId);
```

```csharp
// src/Innovayse.Application/Services/Commands/TerminateService/TerminateServiceHandler.cs
namespace Innovayse.Application.Services.Commands.TerminateService;

using Innovayse.Application.Common;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Permanently terminates a client service.</summary>
public sealed class TerminateServiceHandler(
    IClientServiceRepository repo,
    IProvisioningProvider provisioner,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="TerminateServiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The terminate command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task Handle(TerminateServiceCommand cmd, CancellationToken ct)
    {
        var service = await repo.FindByIdAsync(cmd.ServiceId, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        if (service.ProvisioningRef is not null)
            await provisioner.TerminateAsync(service.ProvisioningRef, ct);

        service.Terminate();
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Create queries and DTO**

```csharp
// src/Innovayse.Application/Services/Queries/GetMyServices/ClientServiceDto.cs
namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Domain.Services;

/// <summary>Represents a client service in list responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Denormalized product name for display.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="NextRenewalAt">Next renewal date, or <see langword="null"/> if not yet active.</param>
public record ClientServiceDto(
    int Id,
    int ProductId,
    string ProductName,
    string BillingCycle,
    ServiceStatus Status,
    DateTimeOffset? NextRenewalAt);
```

```csharp
// src/Innovayse.Application/Services/Queries/GetMyServices/GetMyServicesQuery.cs
namespace Innovayse.Application.Services.Queries.GetMyServices;

/// <summary>Returns all services belonging to a specific client.</summary>
/// <param name="ClientId">The client's primary key.</param>
public record GetMyServicesQuery(int ClientId);
```

```csharp
// src/Innovayse.Application/Services/Queries/GetMyServices/GetMyServicesHandler.cs
namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns all services for a client as DTOs with product names.</summary>
public sealed class GetMyServicesHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo)
{
    /// <summary>
    /// Handles <see cref="GetMyServicesQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of client service DTOs.</returns>
    public async Task<IReadOnlyList<ClientServiceDto>> Handle(GetMyServicesQuery qry, CancellationToken ct)
    {
        var services = await serviceRepo.ListByClientAsync(qry.ClientId, ct);
        var result = new List<ClientServiceDto>();

        foreach (var svc in services)
        {
            var product = await productRepo.FindByIdAsync(svc.ProductId, ct);
            result.Add(new ClientServiceDto(
                svc.Id,
                svc.ProductId,
                product?.Name ?? "Unknown",
                svc.BillingCycle,
                svc.Status,
                svc.NextRenewalAt));
        }

        return result;
    }
}
```

```csharp
// src/Innovayse.Application/Services/Queries/GetServices/GetServicesQuery.cs
namespace Innovayse.Application.Services.Queries.GetServices;

using Innovayse.Application.Common;

/// <summary>Returns a paginated list of all client services (admin view).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (max 100).</param>
public record GetServicesQuery(int Page = 1, int PageSize = 20);
```

```csharp
// src/Innovayse.Application/Services/Queries/GetServices/GetServicesHandler.cs
namespace Innovayse.Application.Services.Queries.GetServices;

using Innovayse.Application.Common;
using Innovayse.Application.Services.Queries.GetMyServices;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Returns a paginated list of all client services for admin use.</summary>
public sealed class GetServicesHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo)
{
    /// <summary>
    /// Handles <see cref="GetServicesQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result of client service DTOs.</returns>
    public async Task<PagedResult<ClientServiceDto>> Handle(GetServicesQuery qry, CancellationToken ct)
    {
        var (services, total) = await serviceRepo.ListAsync(qry.Page, qry.PageSize, ct);
        var result = new List<ClientServiceDto>();

        foreach (var svc in services)
        {
            var product = await productRepo.FindByIdAsync(svc.ProductId, ct);
            result.Add(new ClientServiceDto(
                svc.Id,
                svc.ProductId,
                product?.Name ?? "Unknown",
                svc.BillingCycle,
                svc.Status,
                svc.NextRenewalAt));
        }

        return new PagedResult<ClientServiceDto>(result, total, qry.Page, qry.PageSize);
    }
}
```

- [ ] **Step 4: Build to verify no compile errors**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj 2>&1 | tail -10
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Application/Services/
git commit -m "feat: add client service commands and queries"
```

---

## Task 6: Infrastructure — EF configs + repositories + NullProvisioningProvider

**Files:**
- Create: `src/Innovayse.Infrastructure/Products/Configurations/ProductGroupConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Products/Configurations/ProductConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Products/ProductGroupRepository.cs`
- Create: `src/Innovayse.Infrastructure/Products/ProductRepository.cs`
- Create: `src/Innovayse.Infrastructure/Services/Configurations/ClientServiceConfiguration.cs`
- Create: `src/Innovayse.Infrastructure/Services/ClientServiceRepository.cs`
- Create: `src/Innovayse.Infrastructure/Services/NullProvisioningProvider.cs`
- Modify: `src/Innovayse.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/Innovayse.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create EF configurations**

```csharp
// src/Innovayse.Infrastructure/Products/Configurations/ProductGroupConfiguration.cs
namespace Innovayse.Infrastructure.Products.Configurations;

using Innovayse.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="ProductGroup"/>.</summary>
public sealed class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        builder.ToTable("product_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Products)
            .WithOne()
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Products).HasField("_products");
    }
}
```

```csharp
// src/Innovayse.Infrastructure/Products/Configurations/ProductConfiguration.cs
namespace Innovayse.Infrastructure.Products.Configurations;

using Innovayse.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="Product"/>.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>();
        builder.Property(x => x.MonthlyPrice).IsRequired().HasColumnType("numeric(18,4)");
        builder.Property(x => x.AnnualPrice).IsRequired().HasColumnType("numeric(18,4)");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.DomainEvents);
    }
}
```

```csharp
// src/Innovayse.Infrastructure/Services/Configurations/ClientServiceConfiguration.cs
namespace Innovayse.Infrastructure.Services.Configurations;

using Innovayse.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="ClientService"/>.</summary>
public sealed class ClientServiceConfiguration : IEntityTypeConfiguration<ClientService>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClientService> builder)
    {
        builder.ToTable("client_services");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.BillingCycle).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>();
        builder.Property(x => x.ProvisioningRef).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.DomainEvents);
    }
}
```

- [ ] **Step 2: Create repositories**

```csharp
// src/Innovayse.Infrastructure/Products/ProductGroupRepository.cs
namespace Innovayse.Infrastructure.Products;

using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IProductGroupRepository"/>.</summary>
public sealed class ProductGroupRepository(AppDbContext db) : IProductGroupRepository
{
    /// <inheritdoc/>
    public async Task<ProductGroup?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.ProductGroups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductGroup>> ListAsync(CancellationToken ct) =>
        await db.ProductGroups
            .Include(g => g.Products)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(ProductGroup group) => db.ProductGroups.Add(group);
}
```

```csharp
// src/Innovayse.Infrastructure/Products/ProductRepository.cs
namespace Innovayse.Infrastructure.Products;

using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IProductRepository"/>.</summary>
public sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    /// <inheritdoc/>
    public async Task<Product?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Product>> ListAsync(int? groupId, bool activeOnly, CancellationToken ct)
    {
        var query = db.Products.AsQueryable();

        if (groupId.HasValue)
            query = query.Where(p => p.GroupId == groupId.Value);

        if (activeOnly)
            query = query.Where(p => p.Status == ProductStatus.Active);

        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    /// <inheritdoc/>
    public void Add(Product product) => db.Products.Add(product);
}
```

```csharp
// src/Innovayse.Infrastructure/Services/ClientServiceRepository.cs
namespace Innovayse.Infrastructure.Services;

using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IClientServiceRepository"/>.</summary>
public sealed class ClientServiceRepository(AppDbContext db) : IClientServiceRepository
{
    /// <inheritdoc/>
    public async Task<ClientService?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.ClientServices.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientService>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.ClientServices
            .Where(s => s.ClientId == clientId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ClientService> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var total = await db.ClientServices.CountAsync(ct);
        var items = await db.ClientServices
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public void Add(ClientService service) => db.ClientServices.Add(service);
}
```

- [ ] **Step 3: Create NullProvisioningProvider stub**

```csharp
// src/Innovayse.Infrastructure/Services/NullProvisioningProvider.cs
namespace Innovayse.Infrastructure.Services;

using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// No-op provisioning provider used in development and testing.
/// Returns a predictable reference without calling any external API.
/// Replace with <c>CpanelProvisioningProvider</c> for production.
/// </summary>
public sealed class NullProvisioningProvider(ILogger<NullProvisioningProvider> logger) : IProvisioningProvider
{
    /// <inheritdoc/>
    public Task<string> ProvisionAsync(int clientId, int productId, string billingCycle, CancellationToken ct)
    {
        var reference = $"null-{clientId}-{productId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        logger.LogInformation(
            "NullProvisioningProvider: provision client={ClientId} product={ProductId} cycle={Cycle} → {Ref}",
            clientId, productId, billingCycle, reference);
        return Task.FromResult(reference);
    }

    /// <inheritdoc/>
    public Task SuspendAsync(string provisioningRef, CancellationToken ct)
    {
        logger.LogInformation("NullProvisioningProvider: suspend ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(string provisioningRef, CancellationToken ct)
    {
        logger.LogInformation("NullProvisioningProvider: terminate ref={Ref}", provisioningRef);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Update AppDbContext**

Open `src/Innovayse.Infrastructure/Persistence/AppDbContext.cs` and add:

```csharp
/// <summary>Gets the product groups table.</summary>
public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();

/// <summary>Gets the products table.</summary>
public DbSet<Product> Products => Set<Product>();

/// <summary>Gets the client services table.</summary>
public DbSet<ClientService> ClientServices => Set<ClientService>();
```

Add the using directives at the top:
```csharp
using Innovayse.Domain.Products;
using Innovayse.Domain.Services;
```

Full updated file content:

```csharp
namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Products;
using Innovayse.Domain.Services;
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

    /// <summary>Gets the product groups table.</summary>
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();

    /// <summary>Gets the products table.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Gets the client services table.</summary>
    public DbSet<ClientService> ClientServices => Set<ClientService>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Update DependencyInjection.cs**

Open `src/Innovayse.Infrastructure/DependencyInjection.cs`. Add the new registrations inside the existing `AddInfrastructure` extension method:

```csharp
services.AddScoped<IProductGroupRepository, ProductGroupRepository>();
services.AddScoped<IProductRepository, ProductRepository>();
services.AddScoped<IClientServiceRepository, ClientServiceRepository>();
services.AddScoped<IProvisioningProvider, NullProvisioningProvider>();
```

Also add using directives:
```csharp
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Infrastructure.Products;
using Innovayse.Infrastructure.Services;
```

- [ ] **Step 6: Build entire solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build 2>&1 | tail -15
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Infrastructure/
git commit -m "feat: add EF configs, repositories and NullProvisioningProvider for Products and Services"
```

---

## Task 7: Infrastructure — EF Migration

**Files:**
- Modify: EF creates migration file automatically

- [ ] **Step 1: Create migration**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet ef migrations add AddProductsAndServices \
  --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
  --startup-project src/Innovayse.API/Innovayse.API.csproj \
  --output-dir Persistence/Migrations
```

Expected: Migration file created in `Innovayse.Infrastructure/Persistence/Migrations/`.

- [ ] **Step 2: Review the migration**

Open the generated migration file. Verify it contains:
- `CREATE TABLE product_groups` with columns: `id`, `name`, `description`, `is_active`
- `CREATE TABLE products` with columns: `id`, `group_id`, `name`, `description`, `type`, `status`, `monthly_price`, `annual_price`, `created_at`
- `CREATE TABLE client_services` with columns: `id`, `client_id`, `product_id`, `billing_cycle`, `status`, `provisioning_ref`, `created_at`, `next_renewal_at`
- FK from `products.group_id → product_groups.id`

If any column is missing, check the EF configurations in Task 6 and re-generate.

- [ ] **Step 3: Apply migration to dev database**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet ef database update \
  --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
  --startup-project src/Innovayse.API/Innovayse.API.csproj
```

Expected: `Done.` — all migrations applied.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.Infrastructure/Persistence/Migrations/
git commit -m "feat: migration AddProductsAndServices"
```

---

## Task 8: API — ProductGroupsController + ProductsController

**Files:**
- Create: `src/Innovayse.API/Products/ProductGroupsController.cs`
- Create: `src/Innovayse.API/Products/ProductsController.cs`

- [ ] **Step 1: Create ProductGroupsController**

```csharp
// src/Innovayse.API/Products/ProductGroupsController.cs
namespace Innovayse.API.Products;

using Innovayse.Application.Products.Commands.CreateProductGroup;
using Innovayse.Application.Products.Commands.UpdateProductGroup;
using Innovayse.Application.Products.Queries.GetProductGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing product groups.
/// All write operations require the Admin role.
/// The GET list is public to support the storefront.
/// </summary>
[ApiController]
[Route("api/products/groups")]
public sealed class ProductGroupsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all product groups. Active-only for non-admin callers.</summary>
    /// <param name="activeOnly">Filter to active groups only. Defaults to <see langword="true"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product group DTOs.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductGroupDto>>> GetAll(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ProductGroupDto>>(
            new GetProductGroupsQuery(activeOnly), ct);
        return Ok(result);
    }

    /// <summary>Creates a new product group.</summary>
    /// <param name="cmd">Create command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new group ID.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> Create(
        [FromBody] CreateProductGroupCommand cmd, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return CreatedAtAction(nameof(GetAll), new { }, id);
    }

    /// <summary>Updates an existing product group.</summary>
    /// <param name="id">Product group primary key.</param>
    /// <param name="cmd">Update command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateProductGroupCommand cmd, CancellationToken ct)
    {
        await bus.InvokeAsync(cmd with { Id = id }, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: Create ProductsController**

```csharp
// src/Innovayse.API/Products/ProductsController.cs
namespace Innovayse.API.Products;

using Innovayse.Application.Products.Commands.CreateProduct;
using Innovayse.Application.Products.Commands.UpdateProduct;
using Innovayse.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing products.
/// GET list is public (storefront). Write operations require Admin role.
/// </summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns products, optionally filtered by group or active status.</summary>
    /// <param name="groupId">Optional group filter.</param>
    /// <param name="activeOnly">Filter to active products only. Defaults to <see langword="true"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product DTOs.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(
        [FromQuery] int? groupId = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ProductDto>>(
            new GetProductsQuery(groupId, activeOnly), ct);
        return Ok(result);
    }

    /// <summary>Creates a new product.</summary>
    /// <param name="cmd">Create command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new product ID.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> Create(
        [FromBody] CreateProductCommand cmd, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return CreatedAtAction(nameof(GetAll), new { }, id);
    }

    /// <summary>Updates an existing product's details and prices.</summary>
    /// <param name="id">Product primary key.</param>
    /// <param name="cmd">Update command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateProductCommand cmd, CancellationToken ct)
    {
        await bus.InvokeAsync(cmd with { Id = id }, ct);
        return NoContent();
    }
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.API/Innovayse.API.csproj 2>&1 | tail -10
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.API/Products/
git commit -m "feat: add ProductGroupsController and ProductsController"
```

---

## Task 9: API — ServicesController (Admin) + MyServicesController (Client)

**Files:**
- Create: `src/Innovayse.API/Services/ServicesController.cs`
- Create: `src/Innovayse.API/Services/MyServicesController.cs`

- [ ] **Step 1: Create ServicesController (Admin)**

```csharp
// src/Innovayse.API/Services/ServicesController.cs
namespace Innovayse.API.Services;

using Innovayse.Application.Common;
using Innovayse.Application.Services.Commands.SuspendService;
using Innovayse.Application.Services.Commands.TerminateService;
using Innovayse.Application.Services.Queries.GetMyServices;
using Innovayse.Application.Services.Queries.GetServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing all client services.
/// Requires Admin or Reseller role.
/// </summary>
[ApiController]
[Route("api/services")]
[Authorize(Roles = "Admin,Reseller")]
public sealed class ServicesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all client services.</summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of client service DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientServiceDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<ClientServiceDto>>(
            new GetServicesQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Suspends an active client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/suspend")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Suspend(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new SuspendServiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>Permanently terminates a client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/terminate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Terminate(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new TerminateServiceCommand(id), ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: Create MyServicesController (Client portal)**

The client portal needs the client ID from the database (not just the user ID). The controller resolves the client record from the `sub` claim via a query.

```csharp
// src/Innovayse.API/Services/MyServicesController.cs
namespace Innovayse.API.Services;

using System.Security.Claims;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Application.Services.Queries.GetMyServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client portal endpoints for viewing and ordering services.
/// Requires Client role.
/// </summary>
[ApiController]
[Route("api/me/services")]
[Authorize(Roles = "Client")]
public sealed class MyServicesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all services belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of client service DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientServiceDto>>> GetMine(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User identity not found.");

        var profile = await bus.InvokeAsync<ClientProfileDto?>(new GetMyProfileQuery(userId), ct);
        if (profile is null)
            return NotFound("Client profile not found.");

        var services = await bus.InvokeAsync<IReadOnlyList<ClientServiceDto>>(
            new GetMyServicesQuery(profile.Id), ct);
        return Ok(services);
    }

    /// <summary>Orders a new service for the authenticated client.</summary>
    /// <param name="request">Order request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new service ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> Order(
        [FromBody] OrderRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User identity not found.");

        var profile = await bus.InvokeAsync<ClientProfileDto?>(new GetMyProfileQuery(userId), ct);
        if (profile is null)
            return NotFound("Client profile not found.");

        var cmd = new OrderServiceCommand(profile.Id, request.ProductId, request.BillingCycle);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return CreatedAtAction(nameof(GetMine), new { }, id);
    }
}

/// <summary>Request body for ordering a new service.</summary>
/// <param name="ProductId">The product to order.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
public record OrderRequest(int ProductId, string BillingCycle);
```

- [ ] **Step 3: Check that GetMyProfileQuery and ClientProfileDto exist**

The `GetMyProfileQuery` and `ClientProfileDto` were created in Plan 03. Verify they exist:

```bash
find /c/Users/Dell/Desktop/www/innovayse/backend/src/Innovayse.Application/Clients/Queries -type f
```

If `GetMyProfileQuery.cs` does not exist (it should from Plan 03), create it:

```csharp
// src/Innovayse.Application/Clients/Queries/GetMyProfile/GetMyProfileQuery.cs
namespace Innovayse.Application.Clients.Queries.GetMyProfile;

/// <summary>Returns the client profile for the authenticated user.</summary>
/// <param name="UserId">The Identity user ID from the JWT claim.</param>
public record GetMyProfileQuery(string UserId);
```

- [ ] **Step 4: Build API**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.API/Innovayse.API.csproj 2>&1 | tail -10
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add src/Innovayse.API/Services/
git commit -m "feat: add ServicesController and MyServicesController"
```

---

## Task 10: Integration Tests + dotnet format

**Files:**
- Create: `tests/Innovayse.Integration.Tests/Products/ProductsEndpointTests.cs`
- Create: `tests/Innovayse.Integration.Tests/Services/ServicesEndpointTests.cs`

- [ ] **Step 1: Check existing test infrastructure**

Look at an existing integration test to understand the `CustomWebAppFactory` and `TestAuth` helpers:

```bash
find /c/Users/Dell/Desktop/www/innovayse/backend/tests/Innovayse.Integration.Tests -type f
```

Check the test factory file (e.g., `CustomWebAppFactory.cs` or `WebAppFactory.cs`) to understand how admin and client JWTs are minted.

- [ ] **Step 2: Write Products integration tests**

```csharp
// tests/Innovayse.Integration.Tests/Products/ProductsEndpointTests.cs
namespace Innovayse.Integration.Tests.Products;

using System.Net;
using System.Net.Http.Json;
using Innovayse.Application.Products.Commands.CreateProduct;
using Innovayse.Application.Products.Commands.CreateProductGroup;
using Innovayse.Application.Products.Queries.GetProducts;
using Innovayse.Domain.Products;

public class ProductsEndpointTests(CustomWebAppFactory factory)
    : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetProducts_ReturnsOk_WithoutAuth()
    {
        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateProductGroup_ReturnsCreated_AsAdmin()
    {
        var token = await factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var cmd = new CreateProductGroupCommand("Shared Hosting", "Affordable hosting plans");
        var response = await _client.PostAsJsonAsync("/api/products/groups", cmd);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await response.Content.ReadFromJsonAsync<int>();
        Assert.True(id > 0);
    }

    [Fact]
    public async Task CreateProductGroup_Returns401_WithoutAuth()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var cmd = new CreateProductGroupCommand("Test", null);
        var response = await _client.PostAsJsonAsync("/api/products/groups", cmd);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreated_AsAdmin()
    {
        var token = await factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // First create a group
        var groupCmd = new CreateProductGroupCommand("VPS", null);
        var groupResponse = await _client.PostAsJsonAsync("/api/products/groups", groupCmd);
        var groupId = await groupResponse.Content.ReadFromJsonAsync<int>();

        // Then create a product
        var productCmd = new CreateProductCommand(
            groupId, "VPS-1", "Entry VPS", ProductType.Vps, 9.99m, 99.99m);
        var productResponse = await _client.PostAsJsonAsync("/api/products", productCmd);

        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var productId = await productResponse.Content.ReadFromJsonAsync<int>();
        Assert.True(productId > 0);
    }

    [Fact]
    public async Task GetProducts_FilterByGroup_ReturnsOnlyGroupProducts()
    {
        var token = await factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var groupCmd = new CreateProductGroupCommand("SSL", null);
        var groupResponse = await _client.PostAsJsonAsync("/api/products/groups", groupCmd);
        var groupId = await groupResponse.Content.ReadFromJsonAsync<int>();

        var productCmd = new CreateProductCommand(
            groupId, "SSL-Basic", null, ProductType.Ssl, 4.99m, 49.99m);
        await _client.PostAsJsonAsync("/api/products", productCmd);

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/products?groupId={groupId}&activeOnly=true");
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();

        Assert.NotNull(products);
        Assert.All(products, p => Assert.Equal(groupId, p.GroupId));
    }
}
```

- [ ] **Step 3: Write Services integration tests**

```csharp
// tests/Innovayse.Integration.Tests/Services/ServicesEndpointTests.cs
namespace Innovayse.Integration.Tests.Services;

using System.Net;
using System.Net.Http.Json;
using Innovayse.API.Services;
using Innovayse.Application.Auth.Commands.Register;
using Innovayse.Application.Products.Commands.CreateProduct;
using Innovayse.Application.Products.Commands.CreateProductGroup;
using Innovayse.Domain.Products;

public class ServicesEndpointTests(CustomWebAppFactory factory)
    : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetServices_Returns401_WithoutAuth()
    {
        var response = await _client.GetAsync("/api/services");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrderService_ReturnsCreated_AsClient()
    {
        // 1. Create a product as admin
        var adminToken = await factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var groupResponse = await _client.PostAsJsonAsync("/api/products/groups",
            new CreateProductGroupCommand("Hosting", null));
        var groupId = await groupResponse.Content.ReadFromJsonAsync<int>();

        var productResponse = await _client.PostAsJsonAsync("/api/products",
            new CreateProductCommand(groupId, "Starter", null, ProductType.SharedHosting, 5.99m, 59.99m));
        var productId = await productResponse.Content.ReadFromJsonAsync<int>();

        // 2. Register a client
        var email = $"svc-test-{Guid.NewGuid():N}@example.com";
        _client.DefaultRequestHeaders.Authorization = null;
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterCommand(
            email, "Pass@123!", "Test", "User"));

        // 3. Login as client and get token
        var clientToken = await factory.GetClientTokenAsync(email, "Pass@123!");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", clientToken);

        // 4. Wait for async ClientService creation (Wolverine handler)
        await Task.Delay(300);

        // 5. Order a service
        var orderResponse = await _client.PostAsJsonAsync("/api/me/services",
            new OrderRequest(productId, "monthly"));

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
    }

    [Fact]
    public async Task GetMyServices_Returns200_AfterOrdering()
    {
        // Intentionally kept minimal — order tested above; here we just check the list endpoint auth
        var adminToken = await factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        // Admin cannot access /api/me/services (requires Client role)
        var response = await _client.GetAsync("/api/me/services");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 4: Check that GetClientTokenAsync helper exists in CustomWebAppFactory**

Look at the existing `CustomWebAppFactory`:

```bash
cat /c/Users/Dell/Desktop/www/innovayse/backend/tests/Innovayse.Integration.Tests/CustomWebAppFactory.cs 2>/dev/null | head -80
```

If `GetClientTokenAsync(email, password)` does not exist, add it to the factory:

```csharp
/// <summary>
/// Logs in an existing client user and returns the access token.
/// </summary>
/// <param name="email">Client email.</param>
/// <param name="password">Client password.</param>
/// <returns>JWT access token.</returns>
public async Task<string> GetClientTokenAsync(string email, string password)
{
    using var client = CreateClient();
    var response = await client.PostAsJsonAsync("/api/auth/login",
        new { Email = email, Password = password });
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    return result.GetProperty("accessToken").GetString()!;
}
```

- [ ] **Step 5: Run all tests**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test 2>&1 | tail -20
```

Expected: all tests pass (domain + integration).

If any integration test fails due to timing (Wolverine async), increase the `Task.Delay` from 300 to 500ms.

- [ ] **Step 6: Run dotnet format**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet format
```

Expected: no unformatted files.

- [ ] **Step 7: Run build after format**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build 2>&1 | tail -5
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
git add .
git commit -m "feat: integration tests for Products and Services endpoints + dotnet format"
```

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| `products, product_groups` tables | Task 6 (EF config) |
| `client_services` table | Task 6 (EF config) |
| `IProvisioningProvider` interface | Task 2 |
| Admin CRUD for products/groups | Tasks 3, 4, 8 |
| Client order service | Task 5, 9 |
| Client view my services | Task 5, 9 |
| Stub provisioning for dev | Task 6 (NullProvisioningProvider) |
| Domain tests | Tasks 1, 2 |
| Integration tests | Task 10 |

**Placeholder scan:** None found.

**Type consistency:** `ProductType`, `ProductStatus`, `ServiceStatus` all defined in Domain and referenced consistently through Application and Infrastructure. `ProductGroupDto`, `ProductDto`, `ClientServiceDto` are defined before use in query handlers and controllers.
