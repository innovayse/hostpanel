# Integrations Backend Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 4 REST endpoints under `/api/admin/integrations` that let the admin panel list, read, save, and test-connect 10 third-party integration configurations stored as key-value Settings.

**Architecture:** Pure Clean Architecture — no new Domain entities or Infrastructure files. All logic lives in new Application handlers that reuse `ISettingRepository` and `IUnitOfWork`. The API controller is a thin Wolverine dispatcher following the exact pattern already used by `SettingsController` and `DashboardController`.

**Tech Stack:** ASP.NET Core 8, C# 12, Wolverine 5.x (`IMessageBus.InvokeAsync`), `ISettingRepository` / `IUnitOfWork` (already registered in DI), no new NuGet packages needed.

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `Application/Admin/Integrations/DTOs/IntegrationListItemDto.cs` | Create | Record: slug, name, category, isEnabled, isConfigured |
| `Application/Admin/Integrations/DTOs/IntegrationDetailDto.cs` | Create | Record: slug, isEnabled, fields dict (masked) |
| `Application/Admin/Integrations/DTOs/IntegrationTestResultDto.cs` | Create | Record: success, message, testedAt |
| `Application/Admin/Integrations/Queries/ListIntegrations/ListIntegrationsQuery.cs` | Create | Empty record marker |
| `Application/Admin/Integrations/Queries/ListIntegrations/ListIntegrationsHandler.cs` | Create | Wolverine handler — list all 10 with status |
| `Application/Admin/Integrations/Queries/GetIntegration/GetIntegrationQuery.cs` | Create | Record with Slug |
| `Application/Admin/Integrations/Queries/GetIntegration/GetIntegrationHandler.cs` | Create | Wolverine handler — load + mask secrets |
| `Application/Admin/Integrations/Commands/SaveIntegrationConfig/SaveIntegrationConfigCommand.cs` | Create | Record: slug, isEnabled, fields dict |
| `Application/Admin/Integrations/Commands/SaveIntegrationConfig/SaveIntegrationConfigHandler.cs` | Create | Wolverine handler — upsert settings |
| `Application/Admin/Integrations/Commands/TestIntegrationConnection/TestIntegrationConnectionCommand.cs` | Create | Record with Slug |
| `Application/Admin/Integrations/Commands/TestIntegrationConnection/TestIntegrationConnectionHandler.cs` | Create | Wolverine handler — validate required fields |
| `API/Admin/IntegrationsController.cs` | Create | Thin controller, 4 actions -> bus.InvokeAsync |
| `API/Admin/Requests/SaveIntegrationConfigRequest.cs` | Create | Request body DTO |

Paths above are relative to `backend/src/`. Application files live under `Innovayse.Application/`; API files under `Innovayse.API/`.

---

## Task 1: DTOs

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/DTOs/IntegrationListItemDto.cs`
- Create: `backend/src/Innovayse.Application/Admin/Integrations/DTOs/IntegrationDetailDto.cs`
- Create: `backend/src/Innovayse.Application/Admin/Integrations/DTOs/IntegrationTestResultDto.cs`

- [ ] **Step 1: Create `IntegrationListItemDto.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Summary row returned by the list-integrations endpoint.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration (e.g. "stripe").</param>
/// <param name="Name">Human-readable display name (e.g. "Stripe").</param>
/// <param name="Category">Grouping label (e.g. "Payment Gateways").</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="IsConfigured">Whether all required credential fields are non-empty.</param>
public record IntegrationListItemDto(
    string Slug,
    string Name,
    string Category,
    bool IsEnabled,
    bool IsConfigured);
```

- [ ] **Step 2: Create `IntegrationDetailDto.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Full configuration for one integration, with secret fields masked.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration.</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="Fields">
/// Dictionary mapping field key (e.g. "secret_key") to its stored value.
/// Any field whose key contains "key", "secret", "password", or "token" is
/// returned as "••••••••" when non-empty, or "" when empty.
/// </param>
public record IntegrationDetailDto(
    string Slug,
    bool IsEnabled,
    Dictionary<string, string> Fields);
```

- [ ] **Step 3: Create `IntegrationTestResultDto.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Result of a test-connection request for an integration.
/// </summary>
/// <param name="Success">
/// True when all required fields are configured; otherwise false.
/// </param>
/// <param name="Message">Human-readable explanation of the outcome.</param>
/// <param name="TestedAt">UTC timestamp when the test was performed.</param>
public record IntegrationTestResultDto(
    bool Success,
    string Message,
    DateTimeOffset TestedAt);
```

- [ ] **Step 4: Build to verify compilation**

```bash
cd backend && dotnet build src/Innovayse.Application/Innovayse.Application.csproj --no-incremental -v minimal
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Format**

```bash
cd backend && dotnet format src/Innovayse.Application/Innovayse.Application.csproj
```

- [ ] **Step 6: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/DTOs/
git commit -m "feat(integrations): add DTOs -- IntegrationListItemDto, IntegrationDetailDto, IntegrationTestResultDto"
```

---

## Task 2: ListIntegrations Query + Handler

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Queries/ListIntegrations/ListIntegrationsQuery.cs`
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Queries/ListIntegrations/ListIntegrationsHandler.cs`

- [ ] **Step 1: Create `ListIntegrationsQuery.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Queries.ListIntegrations;

/// <summary>Query that returns the status summary for all 10 integrations.</summary>
public record ListIntegrationsQuery;
```

- [ ] **Step 2: Create `ListIntegrationsHandler.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Queries.ListIntegrations;

using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="ListIntegrationsQuery"/> by reading all integration-prefixed settings
/// and computing the enabled/configured status for each of the 10 known integrations.
/// </summary>
/// <param name="settings">Setting repository for key-value lookups.</param>
public sealed class ListIntegrationsHandler(ISettingRepository settings)
{
    /// <summary>
    /// Static metadata for every integration: display name, category,
    /// required credential fields, and the full set of persisted fields.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Category, string[] RequiredFields, string[] AllFields)> Meta = new()
    {
        ["stripe"]        = ("Stripe",        "Payment Gateways",       ["secret_key"],                                     ["secret_key", "publishable_key", "webhook_secret", "mode"]),
        ["paypal"]        = ("PayPal",        "Payment Gateways",       ["client_id", "client_secret"],                     ["client_id", "client_secret", "mode"]),
        ["bank-transfer"] = ("Bank Transfer", "Payment Gateways",       [],                                                 ["account_name", "iban", "bank_name", "instructions"]),
        ["namecheap"]     = ("Namecheap",     "Domain Registrars",      ["api_key", "api_username", "client_ip"],           ["api_key", "api_username", "client_ip"]),
        ["resellerclub"]  = ("ResellerClub",  "Domain Registrars",      ["reseller_id", "api_key"],                         ["reseller_id", "api_key"]),
        ["enom"]          = ("ENOM",          "Domain Registrars",      ["account_id", "api_key"],                          ["account_id", "api_key"]),
        ["cpanel"]        = ("cPanel WHM",    "Hosting / Provisioning", ["host", "username", "api_token"],                  ["host", "port", "username", "api_token"]),
        ["plesk"]         = ("Plesk",         "Hosting / Provisioning", ["host", "username", "password"],                   ["host", "port", "username", "password"]),
        ["smtp"]          = ("SMTP Server",   "Email / SMTP",           ["host", "username", "password", "from_address"],  ["host", "port", "username", "password", "from_address", "encryption"]),
        ["maxmind"]       = ("MaxMind",       "Fraud Protection",       ["account_id", "license_key"],                      ["account_id", "license_key"]),
    };

    /// <summary>
    /// Returns one <see cref="IntegrationListItemDto"/> per known integration.
    /// </summary>
    /// <param name="query">The list integrations query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of 10 integration summary items.</returns>
    public async Task<IReadOnlyList<IntegrationListItemDto>> HandleAsync(
        ListIntegrationsQuery query, CancellationToken ct)
    {
        var all = await settings.ListAsync(ct);

        // Build a lookup: full key -> value for every integration:* setting.
        var lookup = all
            .Where(s => s.Key.StartsWith("integration:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var result = new List<IntegrationListItemDto>(Meta.Count);

        foreach (var (slug, meta) in Meta)
        {
            var isEnabled = lookup.TryGetValue($"integration:{slug}:is_enabled", out var enabledVal)
                && string.Equals(enabledVal, "true", StringComparison.OrdinalIgnoreCase);

            var isConfigured = meta.RequiredFields.Length == 0
                || meta.RequiredFields.All(field =>
                    lookup.TryGetValue($"integration:{slug}:{field}", out var val)
                    && !string.IsNullOrWhiteSpace(val));

            result.Add(new IntegrationListItemDto(slug, meta.Name, meta.Category, isEnabled, isConfigured));
        }

        return result;
    }
}
```

- [ ] **Step 3: Build**

```bash
cd backend && dotnet build src/Innovayse.Application/Innovayse.Application.csproj --no-incremental -v minimal
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Format**

```bash
cd backend && dotnet format src/Innovayse.Application/Innovayse.Application.csproj
```

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/Queries/ListIntegrations/
git commit -m "feat(integrations): add ListIntegrations query and handler"
```

---

## Task 3: GetIntegration Query + Handler

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetIntegration/GetIntegrationQuery.cs`
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetIntegration/GetIntegrationHandler.cs`

- [ ] **Step 1: Create `GetIntegrationQuery.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Queries.GetIntegration;

/// <summary>Query that returns the full masked configuration for one integration.</summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "stripe".</param>
public record GetIntegrationQuery(string Slug);
```

- [ ] **Step 2: Create `GetIntegrationHandler.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Queries.GetIntegration;

using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="GetIntegrationQuery"/> by loading all settings for the given slug
/// and returning them with secret values masked.
/// </summary>
/// <param name="settings">Setting repository for key-value lookups.</param>
public sealed class GetIntegrationHandler(ISettingRepository settings)
{
    /// <summary>
    /// Substring patterns that identify a field as a secret.
    /// Any field key that contains one of these strings (case-insensitive) will be masked.
    /// </summary>
    private static readonly string[] SecretMarkers = ["key", "secret", "password", "token"];

    /// <summary>
    /// Static metadata for every integration.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Category, string[] RequiredFields, string[] AllFields)> Meta = new()
    {
        ["stripe"]        = ("Stripe",        "Payment Gateways",       ["secret_key"],                                     ["secret_key", "publishable_key", "webhook_secret", "mode"]),
        ["paypal"]        = ("PayPal",        "Payment Gateways",       ["client_id", "client_secret"],                     ["client_id", "client_secret", "mode"]),
        ["bank-transfer"] = ("Bank Transfer", "Payment Gateways",       [],                                                 ["account_name", "iban", "bank_name", "instructions"]),
        ["namecheap"]     = ("Namecheap",     "Domain Registrars",      ["api_key", "api_username", "client_ip"],           ["api_key", "api_username", "client_ip"]),
        ["resellerclub"]  = ("ResellerClub",  "Domain Registrars",      ["reseller_id", "api_key"],                         ["reseller_id", "api_key"]),
        ["enom"]          = ("ENOM",          "Domain Registrars",      ["account_id", "api_key"],                          ["account_id", "api_key"]),
        ["cpanel"]        = ("cPanel WHM",    "Hosting / Provisioning", ["host", "username", "api_token"],                  ["host", "port", "username", "api_token"]),
        ["plesk"]         = ("Plesk",         "Hosting / Provisioning", ["host", "username", "password"],                   ["host", "port", "username", "password"]),
        ["smtp"]          = ("SMTP Server",   "Email / SMTP",           ["host", "username", "password", "from_address"],  ["host", "port", "username", "password", "from_address", "encryption"]),
        ["maxmind"]       = ("MaxMind",       "Fraud Protection",       ["account_id", "license_key"],                      ["account_id", "license_key"]),
    };

    /// <summary>
    /// Loads the configuration for the requested integration and masks secret fields.
    /// </summary>
    /// <param name="query">Query containing the integration slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The integration detail DTO with secret fields masked,
    /// or null if the slug is not recognised.
    /// </returns>
    public async Task<IntegrationDetailDto?> HandleAsync(GetIntegrationQuery query, CancellationToken ct)
    {
        if (!Meta.TryGetValue(query.Slug, out var meta))
            return null;

        var all = await settings.ListAsync(ct);

        var prefix = $"integration:{query.Slug}:";
        var lookup = all
            .Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var isEnabled = lookup.TryGetValue($"{prefix}is_enabled", out var enabledVal)
            && string.Equals(enabledVal, "true", StringComparison.OrdinalIgnoreCase);

        var fields = new Dictionary<string, string>(meta.AllFields.Length);
        foreach (var fieldKey in meta.AllFields)
        {
            lookup.TryGetValue($"{prefix}{fieldKey}", out var raw);
            raw ??= string.Empty;
            fields[fieldKey] = MaskIfSecret(fieldKey, raw);
        }

        return new IntegrationDetailDto(query.Slug, isEnabled, fields);
    }

    /// <summary>
    /// Returns "••••••••" when <paramref name="fieldKey"/> is a secret field and
    /// <paramref name="value"/> is non-empty; otherwise returns <paramref name="value"/> unchanged.
    /// </summary>
    /// <param name="fieldKey">The field key to inspect.</param>
    /// <param name="value">The raw stored value.</param>
    /// <returns>The original value or the masked placeholder.</returns>
    private static string MaskIfSecret(string fieldKey, string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var isSecret = SecretMarkers.Any(marker =>
            fieldKey.Contains(marker, StringComparison.OrdinalIgnoreCase));

        return isSecret ? "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022" : value;
    }
}
```

- [ ] **Step 3: Build**

```bash
cd backend && dotnet build src/Innovayse.Application/Innovayse.Application.csproj --no-incremental -v minimal
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Format**

```bash
cd backend && dotnet format src/Innovayse.Application/Innovayse.Application.csproj
```

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/Queries/GetIntegration/
git commit -m "feat(integrations): add GetIntegration query and handler with secret masking"
```

---

## Task 4: SaveIntegrationConfig Command + Handler

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Commands/SaveIntegrationConfig/SaveIntegrationConfigCommand.cs`
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Commands/SaveIntegrationConfig/SaveIntegrationConfigHandler.cs`

- [ ] **Step 1: Create `SaveIntegrationConfigCommand.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Commands.SaveIntegrationConfig;

/// <summary>
/// Command that upserts all configuration settings for one integration.
/// </summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "stripe".</param>
/// <param name="IsEnabled">Whether the integration should be active after saving.</param>
/// <param name="Fields">
/// Dictionary of field key to value to persist.
/// Fields whose value equals the mask placeholder ("••••••••") are skipped so stored
/// secrets are not overwritten when the admin re-saves without changing them.
/// </param>
public record SaveIntegrationConfigCommand(
    string Slug,
    bool IsEnabled,
    Dictionary<string, string> Fields);
```

- [ ] **Step 2: Create `SaveIntegrationConfigHandler.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Commands.SaveIntegrationConfig;

using Innovayse.Application.Common;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="SaveIntegrationConfigCommand"/> by upserting each field
/// as a key-value <see cref="Setting"/> and persisting in one transaction.
/// </summary>
/// <param name="repo">Setting repository for key-value lookups and inserts.</param>
/// <param name="uow">Unit of work for flushing changes.</param>
public sealed class SaveIntegrationConfigHandler(ISettingRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Placeholder value sent by the frontend when a secret field was not changed.
    /// Receiving this value means "do not overwrite the stored secret."
    /// </summary>
    private const string MaskPlaceholder = "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022";

    /// <summary>
    /// Upserts the is_enabled flag and every supplied field for the given slug,
    /// then saves all changes in a single call.
    /// </summary>
    /// <param name="command">The save integration config command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the settings have been persisted.</returns>
    public async Task HandleAsync(SaveIntegrationConfigCommand command, CancellationToken ct)
    {
        // Upsert the is_enabled flag.
        await UpsertAsync(
            $"integration:{command.Slug}:is_enabled",
            command.IsEnabled ? "true" : "false",
            $"Whether the {command.Slug} integration is enabled.",
            ct);

        // Upsert each supplied field, skipping masked placeholders.
        foreach (var (fieldKey, fieldValue) in command.Fields)
        {
            if (fieldValue == MaskPlaceholder)
                continue; // Frontend sent back a masked value -- do not overwrite stored secret.

            await UpsertAsync(
                $"integration:{command.Slug}:{fieldKey}",
                fieldValue,
                $"{command.Slug} integration field: {fieldKey}.",
                ct);
        }

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Finds an existing setting by <paramref name="key"/> and updates its value,
    /// or creates a new setting when none exists.
    /// </summary>
    /// <param name="key">The full setting key, e.g. integration:stripe:secret_key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="description">Human-readable description stored when creating a new setting.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task UpsertAsync(string key, string value, string description, CancellationToken ct)
    {
        var existing = await repo.FindByKeyAsync(key, ct);
        if (existing is null)
            repo.Add(Setting.Create(key, value, description));
        else
            existing.UpdateValue(value);
    }
}
```

- [ ] **Step 3: Build**

```bash
cd backend && dotnet build src/Innovayse.Application/Innovayse.Application.csproj --no-incremental -v minimal
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Format**

```bash
cd backend && dotnet format src/Innovayse.Application/Innovayse.Application.csproj
```

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/Commands/SaveIntegrationConfig/
git commit -m "feat(integrations): add SaveIntegrationConfig command and handler"
```

---

## Task 5: TestIntegrationConnection Command + Handler

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Commands/TestIntegrationConnection/TestIntegrationConnectionCommand.cs`
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Commands/TestIntegrationConnection/TestIntegrationConnectionHandler.cs`

- [ ] **Step 1: Create `TestIntegrationConnectionCommand.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;

/// <summary>
/// Command that validates whether all required credentials are configured for an integration.
/// Does not make any live network call -- confirms local configuration completeness only.
/// </summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "cpanel".</param>
public record TestIntegrationConnectionCommand(string Slug);
```

- [ ] **Step 2: Create `TestIntegrationConnectionHandler.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;

using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="TestIntegrationConnectionCommand"/> by checking whether all
/// required fields for the integration contain non-empty stored values.
/// </summary>
/// <param name="settings">Setting repository for key-value lookups.</param>
public sealed class TestIntegrationConnectionHandler(ISettingRepository settings)
{
    /// <summary>
    /// Static metadata for every integration.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Category, string[] RequiredFields, string[] AllFields)> Meta = new()
    {
        ["stripe"]        = ("Stripe",        "Payment Gateways",       ["secret_key"],                                     ["secret_key", "publishable_key", "webhook_secret", "mode"]),
        ["paypal"]        = ("PayPal",        "Payment Gateways",       ["client_id", "client_secret"],                     ["client_id", "client_secret", "mode"]),
        ["bank-transfer"] = ("Bank Transfer", "Payment Gateways",       [],                                                 ["account_name", "iban", "bank_name", "instructions"]),
        ["namecheap"]     = ("Namecheap",     "Domain Registrars",      ["api_key", "api_username", "client_ip"],           ["api_key", "api_username", "client_ip"]),
        ["resellerclub"]  = ("ResellerClub",  "Domain Registrars",      ["reseller_id", "api_key"],                         ["reseller_id", "api_key"]),
        ["enom"]          = ("ENOM",          "Domain Registrars",      ["account_id", "api_key"],                          ["account_id", "api_key"]),
        ["cpanel"]        = ("cPanel WHM",    "Hosting / Provisioning", ["host", "username", "api_token"],                  ["host", "port", "username", "api_token"]),
        ["plesk"]         = ("Plesk",         "Hosting / Provisioning", ["host", "username", "password"],                   ["host", "port", "username", "password"]),
        ["smtp"]          = ("SMTP Server",   "Email / SMTP",           ["host", "username", "password", "from_address"],  ["host", "port", "username", "password", "from_address", "encryption"]),
        ["maxmind"]       = ("MaxMind",       "Fraud Protection",       ["account_id", "license_key"],                      ["account_id", "license_key"]),
    };

    /// <summary>
    /// Checks whether all required credential fields for the given integration slug are
    /// stored and non-empty, then returns a result describing the outcome.
    /// </summary>
    /// <param name="command">Command containing the integration slug to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IntegrationTestResultDto"/> with Success = true when fully
    /// configured, or Success = false listing the missing fields.
    /// Returns a not-found failure result when the slug is unrecognised.
    /// </returns>
    public async Task<IntegrationTestResultDto> HandleAsync(
        TestIntegrationConnectionCommand command, CancellationToken ct)
    {
        var testedAt = DateTimeOffset.UtcNow;

        if (!Meta.TryGetValue(command.Slug, out var meta))
        {
            return new IntegrationTestResultDto(
                Success: false,
                Message: $"Unknown integration slug '{command.Slug}'.",
                TestedAt: testedAt);
        }

        // Integrations with no required fields (e.g. bank-transfer) are always "configured".
        if (meta.RequiredFields.Length == 0)
        {
            return new IntegrationTestResultDto(
                Success: true,
                Message: "Connection validated -- all required fields are configured.",
                TestedAt: testedAt);
        }

        var all = await settings.ListAsync(ct);
        var prefix = $"integration:{command.Slug}:";
        var lookup = all
            .Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var missing = meta.RequiredFields
            .Where(field =>
                !lookup.TryGetValue($"{prefix}{field}", out var val)
                || string.IsNullOrWhiteSpace(val))
            .ToList();

        if (missing.Count == 0)
        {
            return new IntegrationTestResultDto(
                Success: true,
                Message: "Connection validated -- all required fields are configured.",
                TestedAt: testedAt);
        }

        var missingList = string.Join(", ", missing);
        return new IntegrationTestResultDto(
            Success: false,
            Message: $"Missing required fields: {missingList}.",
            TestedAt: testedAt);
    }
}
```

- [ ] **Step 3: Build**

```bash
cd backend && dotnet build src/Innovayse.Application/Innovayse.Application.csproj --no-incremental -v minimal
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Format**

```bash
cd backend && dotnet format src/Innovayse.Application/Innovayse.Application.csproj
```

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/Commands/TestIntegrationConnection/
git commit -m "feat(integrations): add TestIntegrationConnection command and handler"
```

---

## Task 6: API Controller + Request DTO

**Files:**
- Create: `backend/src/Innovayse.API/Admin/Requests/SaveIntegrationConfigRequest.cs`
- Create: `backend/src/Innovayse.API/Admin/IntegrationsController.cs`

- [ ] **Step 1: Create `SaveIntegrationConfigRequest.cs`**

```csharp
namespace Innovayse.API.Admin.Requests;

/// <summary>Request body for saving an integration configuration.</summary>
public sealed class SaveIntegrationConfigRequest
{
    /// <summary>Gets or initializes whether the integration should be enabled after saving.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Gets or initializes the field values to persist.
    /// Map of field key (e.g. "secret_key") to value.
    /// Secret fields received as "••••••••" are silently skipped by the handler
    /// so the stored credential is not erased when the admin re-saves the form.
    /// </summary>
    public required Dictionary<string, string> Fields { get; init; }
}
```

- [ ] **Step 2: Create `IntegrationsController.cs`**

```csharp
namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Integrations.Commands.SaveIntegrationConfig;
using Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;
using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Application.Admin.Integrations.Queries.GetIntegration;
using Innovayse.Application.Admin.Integrations.Queries.ListIntegrations;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing third-party integration configurations.
/// Credentials are stored as key-value Settings; secret fields are masked on read.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/integrations")]
[Authorize(Roles = Roles.Admin)]
public sealed class IntegrationsController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Returns the enabled/configured status for all 10 integrations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of integration summary items.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationListItemDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<IntegrationListItemDto>>(
            new ListIntegrationsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns the full configuration for one integration with secret fields masked.
    /// </summary>
    /// <param name="slug">URL-safe integration identifier, e.g. stripe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Integration detail DTO, or 404 when the slug is not recognised.</returns>
    [HttpGet("{slug}")]
    public async Task<ActionResult<IntegrationDetailDto>> GetAsync(string slug, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IntegrationDetailDto?>(
            new GetIntegrationQuery(slug), ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Saves (upserts) the configuration for one integration.
    /// </summary>
    /// <param name="slug">URL-safe integration identifier, e.g. smtp.</param>
    /// <param name="req">Request body containing IsEnabled and Fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{slug}")]
    public async Task<IActionResult> SaveAsync(
        string slug,
        [FromBody] SaveIntegrationConfigRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(
            new SaveIntegrationConfigCommand(slug, req.IsEnabled, req.Fields), ct);
        return NoContent();
    }

    /// <summary>
    /// Tests whether all required credential fields are configured for the integration.
    /// Does not make a live network call.
    /// </summary>
    /// <param name="slug">URL-safe integration identifier, e.g. cpanel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Test result DTO with success flag, message, and timestamp.</returns>
    [HttpPost("{slug}/test")]
    public async Task<ActionResult<IntegrationTestResultDto>> TestAsync(string slug, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IntegrationTestResultDto>(
            new TestIntegrationConnectionCommand(slug), ct);
        return Ok(result);
    }
}
```

- [ ] **Step 3: Build the full solution**

```bash
cd backend && dotnet build src/Innovayse.API/Innovayse.API.csproj --no-incremental -v minimal
```

Expected: `Build succeeded. 0 Error(s)`. Wolverine scans handlers at runtime, not build time, so no handler-discovery warnings are expected here.

- [ ] **Step 4: Format**

```bash
cd backend && dotnet format src/Innovayse.API/Innovayse.API.csproj
```

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.API/Admin/Requests/SaveIntegrationConfigRequest.cs
git add backend/src/Innovayse.API/Admin/IntegrationsController.cs
git commit -m "feat(integrations): add IntegrationsController with list, get, save, and test endpoints"
```

---

## Task 7: Smoke Test via HTTP

No automated test project exists. These manual steps verify the wire-up end-to-end.

- [ ] **Step 1: Start the API**

```bash
cd backend && dotnet run --project src/Innovayse.API/Innovayse.API.csproj
```

Expected: console prints `Now listening on: https://localhost:XXXX` with no Wolverine handler errors in the startup log.

- [ ] **Step 2: Obtain an admin JWT**

Use the existing login endpoint (`POST /api/auth/login`) with an admin account. Copy the returned `accessToken` and substitute it for `{token}` in the steps below.

- [ ] **Step 3: GET /api/admin/integrations**

```
GET https://localhost:{port}/api/admin/integrations
Authorization: Bearer {token}
```

Expected: HTTP 200, JSON array of exactly 10 objects. Check that `bank-transfer` has `"isConfigured": true` (no required fields) while `stripe` has `"isConfigured": false` (not yet configured). Example:

```json
[
  { "slug": "stripe", "name": "Stripe", "category": "Payment Gateways", "isEnabled": false, "isConfigured": false },
  { "slug": "bank-transfer", "name": "Bank Transfer", "category": "Payment Gateways", "isEnabled": false, "isConfigured": true }
]
```

- [ ] **Step 4: PUT /api/admin/integrations/stripe**

```
PUT https://localhost:{port}/api/admin/integrations/stripe
Authorization: Bearer {token}
Content-Type: application/json

{
  "isEnabled": true,
  "fields": {
    "secret_key": "sk_test_abc123",
    "publishable_key": "pk_test_abc123",
    "webhook_secret": "",
    "mode": "test"
  }
}
```

Expected: HTTP 204 No Content.

- [ ] **Step 5: GET /api/admin/integrations/stripe — verify masking**

```
GET https://localhost:{port}/api/admin/integrations/stripe
Authorization: Bearer {token}
```

Expected: HTTP 200.

```json
{
  "slug": "stripe",
  "isEnabled": true,
  "fields": {
    "secret_key": "••••••••",
    "publishable_key": "••••••••",
    "webhook_secret": "",
    "mode": "test"
  }
}
```

Both `secret_key` and `publishable_key` are masked because both contain the substring "key". `webhook_secret` returns `""` (stored as empty, not masked). `mode` returns `"test"` (plain).

- [ ] **Step 6: POST /api/admin/integrations/stripe/test (success)**

```
POST https://localhost:{port}/api/admin/integrations/stripe/test
Authorization: Bearer {token}
```

Expected: HTTP 200.

```json
{
  "success": true,
  "message": "Connection validated -- all required fields are configured.",
  "testedAt": "2026-04-19T10:00:00+00:00"
}
```

`secret_key` is stored and non-empty, so `success: true`.

- [ ] **Step 7: POST /api/admin/integrations/namecheap/test (missing fields)**

```
POST https://localhost:{port}/api/admin/integrations/namecheap/test
Authorization: Bearer {token}
```

Expected: HTTP 200.

```json
{
  "success": false,
  "message": "Missing required fields: api_key, api_username, client_ip.",
  "testedAt": "2026-04-19T10:00:01+00:00"
}
```

- [ ] **Step 8: GET /api/admin/integrations/unknown-slug**

```
GET https://localhost:{port}/api/admin/integrations/unknown-slug
Authorization: Bearer {token}
```

Expected: HTTP 404 Not Found.

- [ ] **Step 9: Final build + format**

```bash
cd backend && dotnet build src/Innovayse.API/Innovayse.API.csproj --no-incremental -v minimal
cd backend && dotnet format src/
```

Expected: `Build succeeded. 0 Error(s)`, and `dotnet format` reports no files changed (everything was formatted after each task).

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(integrations): complete integrations backend module -- 4 endpoints, 10 slugs, secret masking"
```

---

## Self-Review Checklist

### Spec Coverage

| Requirement | Task |
|---|---|
| `GET /api/admin/integrations` list 10 | Task 2 handler + Task 6 controller |
| `GET /api/admin/integrations/{slug}` masked config | Task 3 handler + Task 6 controller |
| `PUT /api/admin/integrations/{slug}` save config | Task 4 handler + Task 6 controller |
| `POST /api/admin/integrations/{slug}/test` validate fields | Task 5 handler + Task 6 controller |
| All 10 slugs in Meta dict | Tasks 2, 3, 5 each carry the full Meta dict |
| Secret masking (key / secret / password / token) | Task 3 `MaskIfSecret` returns "••••••••" |
| Empty secret field returns "" not masked | Task 3 `MaskIfSecret` early return on empty |
| Mask placeholder skip on PUT | Task 4 `MaskPlaceholder` constant + `continue` guard |
| `is_enabled` upserted on save | Task 4 first `UpsertAsync` call |
| `isConfigured` based on RequiredFields all non-empty | Task 2 `.All(...)` LINQ check |
| `Setting.Create` / `UpdateValue` upsert pattern | Task 4 `UpsertAsync` private method |
| `SaveChangesAsync` called once at end | Task 4 -- single call after loop |
| XML docs on all public AND private members | All tasks verified |
| `CancellationToken ct` as last param on every async method | All tasks verified |
| Controller dispatches only to `IMessageBus` | Task 6 -- no direct repo access |
| `[Authorize(Roles = Roles.Admin)]` on controller | Task 6 |
| HTTP 404 for unknown slug on GET | Task 6 `GetAsync` null-check + `NotFound()` |
| Unknown slug in test handler returns `success: false` | Task 5 first `if` block |
| `bank-transfer` `isConfigured: true` (no required fields) | Task 2 `meta.RequiredFields.Length == 0` short-circuit |
| No new Domain / Infrastructure / migration files | Confirmed -- only Application + API layers |
| `dotnet build` verification after each task | Tasks 1-6 each include a build step |
| `dotnet format` after each task | Tasks 1-6 each include a format step |
| Commit after each task | Tasks 1-6 each include a commit step |
