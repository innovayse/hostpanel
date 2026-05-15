# XML Documentation Rules — Innovayse Backend

## Rule

**ALL members — public, protected, internal, private — MUST have XML documentation comments.**

No exceptions by access modifier. Every field, property, method, constructor, class, interface, enum, and enum value gets a doc comment.

## Required Tags

| Member type | Required tags |
|-------------|---------------|
| Class / Record / Interface | `<summary>` |
| Method / Handler | `<summary>`, `<param>` for each param, `<returns>` if non-void |
| Property | `<summary>` |
| Constructor | `<summary>`, `<param>` for each param |
| Enum / Enum value | `<summary>` |
| Exception thrown | `<exception cref="ExceptionType">` |

## Examples

### Class
```csharp
/// <summary>
/// Represents an invoice issued to a client for services rendered.
/// </summary>
public class Invoice : AggregateRoot
```

### Method
```csharp
/// <summary>
/// Adds a line item to the invoice and raises <see cref="InvoiceItemAddedEvent"/>.
/// </summary>
/// <param name="description">Human-readable description of the item.</param>
/// <param name="amount">Unit price in the client's billing currency.</param>
/// <exception cref="InvalidOperationException">
/// Thrown when the invoice is already paid and cannot be modified.
/// </exception>
public void AddItem(string description, decimal amount)
```

### Property
```csharp
/// <summary>Gets the total amount due, including all line items.</summary>
public decimal Total { get; private set; }
```

### Record (Command / DTO)
```csharp
/// <summary>Command to create a new invoice for a client.</summary>
/// <param name="ClientId">The WHMCS-equivalent client identifier.</param>
/// <param name="Items">Line items to include on the invoice.</param>
public record CreateInvoiceCommand(int ClientId, List<InvoiceItemDto> Items);
```

### Private field / method
```csharp
/// <summary>Line items that make up this invoice.</summary>
private readonly List<InvoiceItem> _items = [];

/// <summary>Recalculates <see cref="Total"/> from all current line items.</summary>
private void RecalculateTotal()
{
    Total = _items.Sum(i => i.Amount);
}
```

### Interface
```csharp
/// <summary>Abstraction over a payment gateway provider (Stripe, PayPal, etc.).</summary>
public interface IPaymentGateway
{
    /// <summary>Charges the client's default payment method.</summary>
    /// <param name="request">Charge details including amount and currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the transaction ID and status.</returns>
    Task<PaymentResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
}
```

## What NOT to document

- Test methods — skip XML docs in test projects
- Auto-generated / migration files — skip

## Enforcement

`.editorconfig` sets:
```ini
dotnet_diagnostic.CS1591.severity = warning
```

Add to each `.csproj` to treat as error in CI:
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <NoWarn>$(NoWarn)</NoWarn>
</PropertyGroup>
```
