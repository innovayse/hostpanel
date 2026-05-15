# CWP Apps & Integrations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CWP plugin auto-appears in Apps & Integrations UI after Plugin Manager install, with a branded config page (dark header, live status sidebar) and server-info endpoint.

**Architecture:** Plugin-first override — `plugin.json` gains `showInIntegrations: true`, `PluginLoader` merges plugin into integrations list, `IntegrationDetailView` detects slug `"cwp"` and renders `CwpIntegrationPage` instead of generic view. New `GET /api/admin/integrations/cwp/server-info` endpoint fetches live CWP stats, cached 5 min, invalidated on test/save.

**Tech Stack:** ASP.NET Core 8, Wolverine, IMemoryCache, EF Core (settings read), Vue 3 + `<script setup lang="ts">`, Pinia, Tailwind CSS, shadcn-vue

---

## File Map

### Backend — New
| File | Responsibility |
|------|----------------|
| `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/CwpServerInfoDto.cs` | DTO returned by server-info endpoint |
| `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoQuery.cs` | Wolverine query record |
| `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoHandler.cs` | Fetches config from settings, calls CWP API, caches result |

### Backend — Modified
| File | Change |
|------|--------|
| `backend/src/Innovayse.Providers.CWP/plugin.json` | Add `showInIntegrations`, `integrationsCategory` |
| `backend/src/Innovayse.Infrastructure/Plugins/PluginLoader.cs` | Read new fields, register plugin into integrations source |
| `backend/src/Innovayse.API/Admin/IntegrationsController.cs` | Add `GET /{slug}/server-info` route for cwp; invalidate cache in test/save handlers |

### Frontend — New
| File | Responsibility |
|------|----------------|
| `admin/src/modules/integrations/types/cwp.types.ts` | `CwpServerInfoDto` TypeScript interface |
| `admin/src/modules/integrations/composables/useCwpServerInfo.ts` | Fetch + reactive state for server-info |
| `admin/src/modules/integrations/components/CwpBrandHeader.vue` | Dark banner with CWP branding + animated status dot |
| `admin/src/modules/integrations/components/CwpStatusSidebar.vue` | Live status: connected badge, accounts, version, last tested |
| `admin/src/modules/integrations/views/CwpIntegrationPage.vue` | Page shell: `CwpBrandHeader` + `IntegrationConfigForm` + `CwpStatusSidebar` |

### Frontend — Modified
| File | Change |
|------|--------|
| `admin/src/modules/integrations/types/integration.types.ts` | Add `isPlugin?: boolean` to `IntegrationDto` |
| `admin/src/modules/integrations/views/IntegrationDetailView.vue` | Detect slug `"cwp"`, load `CwpIntegrationPage`; redirect if plugin not installed |

---

## Task 1: Add `showInIntegrations` fields to plugin.json

**Files:**
- Modify: `backend/src/Innovayse.Providers.CWP/plugin.json`

- [ ] **Step 1: Update plugin.json**

Replace the file content with:

```json
{
  "id": "innovayse-cwp",
  "name": "CentOS Web Panel",
  "version": "1.0.0",
  "author": "Innovayse",
  "description": "Provisioning provider for CentOS Web Panel (CWP).",
  "type": "Provisioning",
  "category": "Hosting / Provisioning",
  "color": "#1a73e8",
  "entryPoint": "Innovayse.Providers.CWP.CwpProvisioningProvider",
  "sdkVersion": "1.0",
  "showInIntegrations": true,
  "integrationsCategory": "Provisioning",
  "fields": [
    { "key": "host",    "label": "CWP Host", "type": "text",   "required": true  },
    { "key": "port",    "label": "Port",      "type": "text",   "required": false },
    { "key": "api_key", "label": "API Key",   "type": "secret", "required": true  }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/src/Innovayse.Providers.CWP/plugin.json
git commit -m "feat(cwp): add showInIntegrations fields to plugin.json"
```

---

## Task 2: Update PluginLoader to register plugins in integrations source

**Files:**
- Modify: `backend/src/Innovayse.Infrastructure/Plugins/PluginLoader.cs`

- [ ] **Step 1: Read PluginLoader.cs current content**

Open `backend/src/Innovayse.Infrastructure/Plugins/PluginLoader.cs` and locate the manifest deserialization block — find where `plugin.json` fields are read into the manifest object.

- [ ] **Step 2: Add `ShowInIntegrations` and `IntegrationsCategory` to the manifest model**

In `backend/src/Innovayse.SDK/Plugins/PluginManifest.cs` (or wherever the manifest record/class lives), add two properties:

```csharp
/// <summary>Gets a value indicating whether this plugin should appear in the Apps &amp; Integrations list.</summary>
public bool ShowInIntegrations { get; init; }

/// <summary>Gets the category name shown in the integrations grid (e.g. "Provisioning").</summary>
public string? IntegrationsCategory { get; init; }
```

- [ ] **Step 3: In PluginLoader, after loading each manifest, register integration entry**

Find the loop that loads each plugin. After the manifest is parsed and before or after DI registration, add:

```csharp
if (manifest.ShowInIntegrations)
{
    integrationRegistry.Register(new PluginIntegrationEntry(
        Slug: manifest.Id,
        Name: manifest.Name,
        Category: manifest.IntegrationsCategory ?? manifest.Category,
        Color: manifest.Color,
        FieldDefinitions: manifest.Fields
            .Select(f => new FieldDefinitionDto(f.Key, f.Label, f.Type, f.Required))
            .ToList()
    ));
}
```

- [ ] **Step 4: Create `PluginIntegrationEntry` record and `IPluginIntegrationRegistry` interface**

Create `backend/src/Innovayse.Application/Admin/Integrations/IPluginIntegrationRegistry.cs`:

```csharp
namespace Innovayse.Application.Admin.Integrations;

/// <summary>Registry of plugins that expose themselves as integrations in the admin UI.</summary>
public interface IPluginIntegrationRegistry
{
    /// <summary>Registers a plugin as an integration entry.</summary>
    /// <param name="entry">The plugin integration metadata.</param>
    void Register(PluginIntegrationEntry entry);

    /// <summary>Returns all registered plugin integration entries.</summary>
    /// <returns>Read-only list of plugin integration entries.</returns>
    IReadOnlyList<PluginIntegrationEntry> GetAll();

    /// <summary>Returns true if a plugin with the given slug is registered.</summary>
    /// <param name="slug">The plugin identifier slug.</param>
    /// <returns>True if registered, false otherwise.</returns>
    bool IsRegistered(string slug);
}

/// <summary>Metadata for a plugin that appears in the integrations list.</summary>
/// <param name="Slug">Unique plugin identifier used as URL slug.</param>
/// <param name="Name">Display name shown in the integrations grid.</param>
/// <param name="Category">Category name for grouping (e.g. "Provisioning").</param>
/// <param name="Color">Brand hex color (e.g. "#1a73e8").</param>
/// <param name="FieldDefinitions">Config field definitions for the dynamic form.</param>
public record PluginIntegrationEntry(
    string Slug,
    string Name,
    string Category,
    string Color,
    IReadOnlyList<FieldDefinitionDto> FieldDefinitions);
```

Create `backend/src/Innovayse.Infrastructure/Plugins/PluginIntegrationRegistry.cs`:

```csharp
namespace Innovayse.Infrastructure.Plugins;

/// <summary>In-memory registry of plugins that expose themselves as integrations.</summary>
public sealed class PluginIntegrationRegistry : IPluginIntegrationRegistry
{
    /// <summary>Thread-safe list of registered plugin integration entries.</summary>
    private readonly List<PluginIntegrationEntry> _entries = [];

    /// <inheritdoc/>
    public void Register(PluginIntegrationEntry entry) => _entries.Add(entry);

    /// <inheritdoc/>
    public IReadOnlyList<PluginIntegrationEntry> GetAll() => _entries.AsReadOnly();

    /// <inheritdoc/>
    public bool IsRegistered(string slug) =>
        _entries.Any(e => e.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}
```

Register as singleton in the DI module (wherever plugins are registered):

```csharp
services.AddSingleton<IPluginIntegrationRegistry, PluginIntegrationRegistry>();
```

- [ ] **Step 5: Build and verify no errors**

```bash
cd backend
dotnet build src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/IPluginIntegrationRegistry.cs
git add backend/src/Innovayse.Infrastructure/Plugins/PluginIntegrationRegistry.cs
git add backend/src/Innovayse.Infrastructure/Plugins/PluginLoader.cs
git add backend/src/Innovayse.SDK/Plugins/PluginManifest.cs
git commit -m "feat(plugins): register showInIntegrations plugins in IPluginIntegrationRegistry"
```

---

## Task 3: Merge plugin integrations into GET /api/admin/integrations list

**Files:**
- Modify: `backend/src/Innovayse.Application/Admin/Integrations/Queries/ListIntegrations/ListIntegrationsHandler.cs`
- Modify: `backend/src/Innovayse.Application/Admin/Integrations/DTOs/IntegrationListItemDto.cs`

- [ ] **Step 1: Add `IsPlugin` field to `IntegrationListItemDto`**

```csharp
/// <summary>Summary item for the integrations list grid.</summary>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Category">Grouping category.</param>
/// <param name="IsEnabled">Whether the integration is active.</param>
/// <param name="IsConfigured">Whether all required fields are filled.</param>
/// <param name="IsPlugin">True if this entry comes from an installed plugin (not built-in).</param>
public record IntegrationListItemDto(
    string Slug,
    string Name,
    string Category,
    bool IsEnabled,
    bool IsConfigured,
    bool IsPlugin = false);
```

- [ ] **Step 2: Inject `IPluginIntegrationRegistry` into `ListIntegrationsHandler` and merge entries**

Find the handler's `Handle` method. After building the list of built-in integrations, append plugin entries:

```csharp
var pluginEntries = _pluginRegistry.GetAll()
    .Select(p => new IntegrationListItemDto(
        Slug: p.Slug,
        Name: p.Name,
        Category: p.Category,
        IsEnabled: IsIntegrationEnabled(p.Slug),
        IsConfigured: AreRequiredFieldsConfigured(p.Slug, p.FieldDefinitions),
        IsPlugin: true))
    .ToList();

return [..builtInEntries, ..pluginEntries];
```

- [ ] **Step 3: Build**

```bash
dotnet build src/Innovayse.Application/Innovayse.Application.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/Queries/ListIntegrations/ListIntegrationsHandler.cs
git add backend/src/Innovayse.Application/Admin/Integrations/DTOs/IntegrationListItemDto.cs
git commit -m "feat(integrations): merge plugin entries into integrations list"
```

---

## Task 4: Create `CwpServerInfoDto` and `GetCwpServerInfoQuery`

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/CwpServerInfoDto.cs`
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoQuery.cs`

- [ ] **Step 1: Create DTO**

```csharp
// backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/CwpServerInfoDto.cs
namespace Innovayse.Application.Admin.Integrations.Queries.GetCwpServerInfo;

/// <summary>Live server status returned by the CWP server-info endpoint.</summary>
/// <param name="Connected">True if the API connection succeeded.</param>
/// <param name="AccountsCount">Total hosting accounts on the server, or null if unavailable.</param>
/// <param name="CwpVersion">CWP software version string, or null if unavailable.</param>
/// <param name="LastTestedAt">UTC timestamp of the last successful connection test, or null.</param>
/// <param name="ErrorMessage">Human-readable error if Connected is false, otherwise null.</param>
public record CwpServerInfoDto(
    bool Connected,
    int? AccountsCount,
    string? CwpVersion,
    DateTimeOffset? LastTestedAt,
    string? ErrorMessage);
```

- [ ] **Step 2: Create query record**

```csharp
// backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoQuery.cs
namespace Innovayse.Application.Admin.Integrations.Queries.GetCwpServerInfo;

/// <summary>Query to fetch live CWP server status and metadata.</summary>
public record GetCwpServerInfoQuery;
```

- [ ] **Step 3: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/
git commit -m "feat(cwp): add CwpServerInfoDto and GetCwpServerInfoQuery"
```

---

## Task 5: Implement `GetCwpServerInfoHandler`

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoHandler.cs`

- [ ] **Step 1: Write the failing test**

Create `backend/tests/Innovayse.Application.Tests/Admin/Integrations/GetCwpServerInfoHandlerTests.cs`:

```csharp
namespace Innovayse.Application.Tests.Admin.Integrations;

public class GetCwpServerInfoHandlerTests
{
    [Fact]
    public async Task Handle_WhenConfigMissing_ReturnsNotConnected()
    {
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.GetValueAsync("integration:innovayse-cwp:host", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var cwpClient = Substitute.For<ICwpApiClient>();
        var handler = new GetCwpServerInfoHandler(settingsRepo, cwpClient, cache);

        var result = await handler.Handle(new GetCwpServerInfoQuery(), CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorMessage.Should().Be("CWP is not configured.");
    }

    [Fact]
    public async Task Handle_WhenApiSucceeds_ReturnsConnectedWithData()
    {
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.GetValueAsync("integration:innovayse-cwp:host", Arg.Any<CancellationToken>())
            .Returns("192.168.1.1");
        settingsRepo.GetValueAsync("integration:innovayse-cwp:port", Arg.Any<CancellationToken>())
            .Returns("2304");
        settingsRepo.GetValueAsync("integration:innovayse-cwp:api_key", Arg.Any<CancellationToken>())
            .Returns("secret123");

        var cache = new MemoryCache(new MemoryCacheOptions());
        var cwpClient = Substitute.For<ICwpApiClient>();
        cwpClient.GetServerInfoAsync("192.168.1.1", "2304", "secret123", Arg.Any<CancellationToken>())
            .Returns(new CwpServerInfoResult(AccountsCount: 42, CwpVersion: "1.9.1"));

        var handler = new GetCwpServerInfoHandler(settingsRepo, cwpClient, cache);

        var result = await handler.Handle(new GetCwpServerInfoQuery(), CancellationToken.None);

        result.Connected.Should().BeTrue();
        result.AccountsCount.Should().Be(42);
        result.CwpVersion.Should().Be("1.9.1");
        result.ErrorMessage.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

```bash
cd backend
dotnet test tests/Innovayse.Application.Tests/ --filter "GetCwpServerInfoHandlerTests" -v
```

Expected: FAIL — `GetCwpServerInfoHandler` not found.

- [ ] **Step 3: Create `CwpServerInfoResult` record in CWP provider**

Add to `backend/src/Innovayse.Providers.CWP/Models/CwpServerInfoResult.cs`:

```csharp
namespace Innovayse.Providers.CWP.Models;

/// <summary>Parsed result from CWP server info API call.</summary>
/// <param name="AccountsCount">Total account count on the server.</param>
/// <param name="CwpVersion">CWP software version string.</param>
public record CwpServerInfoResult(int AccountsCount, string CwpVersion);
```

- [ ] **Step 4: Add `GetServerInfoAsync` to `CwpApiClient`**

In `backend/src/Innovayse.Providers.CWP/CwpApiClient.cs`, add:

```csharp
/// <summary>Fetches server metadata including account count and CWP version.</summary>
/// <param name="host">CWP server hostname or IP.</param>
/// <param name="port">CWP API port (default 2304).</param>
/// <param name="apiKey">CWP API key.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Server info result with account count and version.</returns>
/// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
public async Task<CwpServerInfoResult> GetServerInfoAsync(
    string host, string port, string apiKey, CancellationToken ct)
{
    var url = $"https://{host}:{port}/v1/account?key={apiKey}&action=list";
    var response = await _http.GetFromJsonAsync<CwpListResponse>(url, ct)
        ?? throw new HttpRequestException("Empty response from CWP API.");
    return new CwpServerInfoResult(
        AccountsCount: response.Accounts?.Length ?? 0,
        CwpVersion: response.CwpVersion ?? "unknown");
}
```

Also add `ICwpApiClient` interface to `backend/src/Innovayse.SDK/Plugins/ICwpApiClient.cs`:

```csharp
namespace Innovayse.SDK.Plugins;

/// <summary>HTTP client abstraction for the CWP REST API.</summary>
public interface ICwpApiClient
{
    /// <summary>Fetches server metadata including account count and CWP version.</summary>
    /// <param name="host">CWP server hostname or IP.</param>
    /// <param name="port">CWP API port.</param>
    /// <param name="apiKey">CWP API key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Server info result with account count and version.</returns>
    Task<CwpServerInfoResult> GetServerInfoAsync(string host, string port, string apiKey, CancellationToken ct);
}
```

- [ ] **Step 5: Implement `GetCwpServerInfoHandler`**

```csharp
// backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoHandler.cs
namespace Innovayse.Application.Admin.Integrations.Queries.GetCwpServerInfo;

/// <summary>Handles <see cref="GetCwpServerInfoQuery"/> — fetches live CWP server status, cached 5 minutes.</summary>
public sealed class GetCwpServerInfoHandler(
    ISettingsRepository settings,
    ICwpApiClient cwpClient,
    IMemoryCache cache)
{
    /// <summary>Cache key for CWP server info.</summary>
    private const string CacheKey = "cwp:server-info";

    /// <summary>Fetches CWP server status from cache or live API.</summary>
    /// <param name="query">The query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>CWP server info DTO.</returns>
    public async Task<CwpServerInfoDto> Handle(GetCwpServerInfoQuery query, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out CwpServerInfoDto? cached) && cached is not null)
            return cached;

        var host    = await settings.GetValueAsync("integration:innovayse-cwp:host", ct);
        var port    = await settings.GetValueAsync("integration:innovayse-cwp:port", ct) ?? "2304";
        var apiKey  = await settings.GetValueAsync("integration:innovayse-cwp:api_key", ct);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new CwpServerInfoDto(
                Connected: false,
                AccountsCount: null,
                CwpVersion: null,
                LastTestedAt: null,
                ErrorMessage: "CWP is not configured.");
        }

        try
        {
            var result = await cwpClient.GetServerInfoAsync(host, port, apiKey, ct);
            var dto = new CwpServerInfoDto(
                Connected: true,
                AccountsCount: result.AccountsCount,
                CwpVersion: result.CwpVersion,
                LastTestedAt: DateTimeOffset.UtcNow,
                ErrorMessage: null);

            cache.Set(CacheKey, dto, TimeSpan.FromMinutes(5));
            return dto;
        }
        catch (Exception ex)
        {
            return new CwpServerInfoDto(
                Connected: false,
                AccountsCount: null,
                CwpVersion: null,
                LastTestedAt: null,
                ErrorMessage: ex.Message);
        }
    }
}
```

- [ ] **Step 6: Run tests — verify they pass**

```bash
dotnet test tests/Innovayse.Application.Tests/ --filter "GetCwpServerInfoHandlerTests" -v
```

Expected: 2 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Innovayse.Application/Admin/Integrations/Queries/GetCwpServerInfo/
git add backend/src/Innovayse.Providers.CWP/Models/CwpServerInfoResult.cs
git add backend/src/Innovayse.SDK/Plugins/ICwpApiClient.cs
git add backend/src/Innovayse.Providers.CWP/CwpApiClient.cs
git add backend/tests/Innovayse.Application.Tests/Admin/Integrations/GetCwpServerInfoHandlerTests.cs
git commit -m "feat(cwp): implement GetCwpServerInfoHandler with caching"
```

---

## Task 6: Add `GET /api/admin/integrations/cwp/server-info` endpoint

**Files:**
- Modify: `backend/src/Innovayse.API/Admin/IntegrationsController.cs`

- [ ] **Step 1: Add the endpoint and cache invalidation on test**

In `IntegrationsController`, add after the existing endpoints:

```csharp
/// <summary>Returns live CWP server status (cached 5 minutes).</summary>
/// <param name="ct">Cancellation token.</param>
/// <returns>CWP server info including connection status, account count, and version.</returns>
[HttpGet("cwp/server-info")]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<CwpServerInfoDto>> GetCwpServerInfo(CancellationToken ct)
{
    var result = await _bus.InvokeAsync<CwpServerInfoDto>(new GetCwpServerInfoQuery(), ct);
    return Ok(result);
}
```

Also, in the existing test endpoint handler or `POST /{slug}/test` action, add cache invalidation after the test runs:

```csharp
_cache.Remove("cwp:server-info");
```

And in `PUT /{slug}` save action, add the same:

```csharp
if (slug.Equals("cwp", StringComparison.OrdinalIgnoreCase))
    _cache.Remove("cwp:server-info");
```

Inject `IMemoryCache` into the controller via primary constructor:

```csharp
public class IntegrationsController(IMessageBus bus, IMemoryCache cache) : ControllerBase
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Innovayse.API/Innovayse.API.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Run dotnet format**

```bash
dotnet format src/Innovayse.API/Innovayse.API.csproj
```

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.API/Admin/IntegrationsController.cs
git commit -m "feat(api): add GET /integrations/cwp/server-info endpoint"
```

---

## Task 7: Add `CwpServerInfoDto` TypeScript type

**Files:**
- Create: `admin/src/modules/integrations/types/cwp.types.ts`
- Modify: `admin/src/modules/integrations/types/integration.types.ts`

- [ ] **Step 1: Create `cwp.types.ts`**

```typescript
/**
 * Live CWP server status returned by the server-info endpoint.
 */
export interface CwpServerInfoDto {
  /** True if the API connection succeeded. */
  connected: boolean
  /** Total hosting accounts on the server, null if unavailable. */
  accountsCount: number | null
  /** CWP software version string, null if unavailable. */
  cwpVersion: string | null
  /** ISO 8601 timestamp of the last successful connection test, null if never tested. */
  lastTestedAt: string | null
  /** Human-readable error message if connected is false, otherwise null. */
  errorMessage: string | null
}
```

- [ ] **Step 2: Add `isPlugin` to `IntegrationDto` in `integration.types.ts`**

Find the `IntegrationDto` interface and add one field:

```typescript
export interface IntegrationDto {
  slug: string
  name: string
  description?: string
  category: string
  isEnabled: boolean
  lastTestedAt: string | null
  /** True if this integration comes from an installed plugin (not built-in). */
  isPlugin?: boolean
}
```

- [ ] **Step 3: Commit**

```bash
git add admin/src/modules/integrations/types/cwp.types.ts
git add admin/src/modules/integrations/types/integration.types.ts
git commit -m "feat(cwp): add CwpServerInfoDto type and isPlugin flag"
```

---

## Task 8: Create `useCwpServerInfo` composable

**Files:**
- Create: `admin/src/modules/integrations/composables/useCwpServerInfo.ts`

- [ ] **Step 1: Create composable**

```typescript
import { ref } from 'vue'
import type { CwpServerInfoDto } from '../types/cwp.types'
import { useApi } from '@/composables/useApi'

/**
 * Fetches and manages live CWP server status from the server-info endpoint.
 *
 * Handles loading and error states automatically. Call `fetch()` to load data.
 *
 * @returns Reactive server info, loading flag, error message, and fetch action.
 */
export function useCwpServerInfo() {
  /** CWP server info data, null until first successful fetch. */
  const info = ref<CwpServerInfoDto | null>(null)

  /** True while a fetch is in progress. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches live CWP server status from the backend.
   *
   * On network failure, sets error and leaves info as previous value.
   * Never throws — safe to call without try/catch.
   *
   * @returns Promise that resolves when the fetch completes.
   */
  async function fetch(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      info.value = await useApi<CwpServerInfoDto>('/admin/integrations/cwp/server-info')
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch CWP server info.'
    } finally {
      loading.value = false
    }
  }

  return { info, loading, error, fetch }
}
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/composables/useCwpServerInfo.ts
git commit -m "feat(cwp): add useCwpServerInfo composable"
```

---

## Task 9: Create `CwpBrandHeader` component

**Files:**
- Create: `admin/src/modules/integrations/components/CwpBrandHeader.vue`

- [ ] **Step 1: Create component**

```vue
<script setup lang="ts">
/**
 * Dark branded header for the CWP integration config page.
 *
 * Shows CWP logo, title, version badge, and an animated connection status dot.
 */

/** Props for CwpBrandHeader. */
const props = defineProps<{
  /** CWP version string to display in the badge, or null while loading. */
  version: string | null
  /** Connection status — controls dot color and animation. */
  status: 'connected' | 'error' | 'unknown'
}>()
</script>

<template>
  <div class="flex items-center justify-between rounded-xl bg-gray-900 px-6 py-5 border-l-4 border-[#1a73e8]">
    <div class="flex items-center gap-4">
      <div class="flex h-12 w-12 items-center justify-center rounded-lg bg-[#1a73e8]">
        <span class="text-2xl font-bold text-white">C</span>
      </div>
      <div>
        <h1 class="text-xl font-semibold text-white">CentOS Web Panel</h1>
        <p class="text-sm text-gray-400">Provisioning Provider</p>
      </div>
      <span
        v-if="version"
        class="ml-2 rounded-full bg-gray-700 px-3 py-0.5 text-xs text-gray-300"
      >
        v{{ version }}
      </span>
    </div>
    <div class="flex items-center gap-2">
      <span
        :class="[
          'h-3 w-3 rounded-full',
          status === 'connected' && 'bg-green-400 animate-pulse',
          status === 'error'     && 'bg-red-500',
          status === 'unknown'   && 'bg-gray-500',
        ]"
      />
      <span class="text-sm" :class="{
        'text-green-400': status === 'connected',
        'text-red-400':   status === 'error',
        'text-gray-400':  status === 'unknown',
      }">
        {{ status === 'connected' ? 'Connected' : status === 'error' ? 'Connection Error' : 'Unknown' }}
      </span>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/components/CwpBrandHeader.vue
git commit -m "feat(cwp): add CwpBrandHeader component"
```

---

## Task 10: Create `CwpStatusSidebar` component

**Files:**
- Create: `admin/src/modules/integrations/components/CwpStatusSidebar.vue`

- [ ] **Step 1: Create component**

```vue
<script setup lang="ts">
/**
 * Live status sidebar for the CWP integration config page.
 *
 * Displays connection badge, accounts count, CWP version, and last tested time.
 * Emits `test` when the user clicks Test Connection.
 */
import { computed } from 'vue'
import type { CwpServerInfoDto } from '../types/cwp.types'

/** Props for CwpStatusSidebar. */
const props = defineProps<{
  /** CWP server info data, null while loading or on error. */
  info: CwpServerInfoDto | null
  /** True while a test connection request is in progress. */
  testing: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user clicks the Test Connection button. */
  test: []
}>()

/** Human-readable relative time since last test. */
const lastTestedLabel = computed(() => {
  if (!props.info?.lastTestedAt) return 'Never'
  const diff = Math.floor((Date.now() - new Date(props.info.lastTestedAt).getTime()) / 1000)
  if (diff < 60) return 'Just now'
  if (diff < 3600) return `${Math.floor(diff / 60)} minutes ago`
  return `${Math.floor(diff / 3600)} hours ago`
})
</script>

<template>
  <div class="rounded-xl border border-gray-200 bg-white p-5 space-y-5 dark:border-gray-700 dark:bg-gray-800">
    <!-- Connection badge -->
    <div class="flex items-center gap-2">
      <span
        :class="[
          'h-2.5 w-2.5 rounded-full',
          info?.connected ? 'bg-green-500 animate-pulse' : 'bg-gray-400',
        ]"
      />
      <span class="text-sm font-medium" :class="info?.connected ? 'text-green-600' : 'text-gray-500'">
        {{ info?.connected ? 'Connected' : info ? 'Not Connected' : 'Status Unknown' }}
      </span>
    </div>

    <!-- Accounts count -->
    <div v-if="info?.connected">
      <p class="text-xs uppercase tracking-wide text-gray-400">Accounts</p>
      <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ info.accountsCount ?? '—' }}</p>
    </div>

    <!-- CWP Version -->
    <div v-if="info?.cwpVersion">
      <p class="text-xs uppercase tracking-wide text-gray-400">CWP Version</p>
      <p class="text-sm font-medium text-gray-700 dark:text-gray-200">{{ info.cwpVersion }}</p>
    </div>

    <!-- Last tested -->
    <div>
      <p class="text-xs uppercase tracking-wide text-gray-400">Last Tested</p>
      <p class="text-sm text-gray-600 dark:text-gray-300">{{ lastTestedLabel }}</p>
    </div>

    <!-- Error message -->
    <p v-if="info && !info.connected && info.errorMessage" class="text-xs text-red-500">
      {{ info.errorMessage }}
    </p>

    <!-- Test button -->
    <button
      class="w-full rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 dark:border-gray-600 dark:text-gray-200 dark:hover:bg-gray-700"
      :disabled="testing"
      @click="emit('test')"
    >
      <span v-if="testing">Testing…</span>
      <span v-else>Test Connection</span>
    </button>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/components/CwpStatusSidebar.vue
git commit -m "feat(cwp): add CwpStatusSidebar component"
```

---

## Task 11: Create `CwpIntegrationPage` view

**Files:**
- Create: `admin/src/modules/integrations/views/CwpIntegrationPage.vue`

- [ ] **Step 1: Create the page**

```vue
<script setup lang="ts">
/**
 * Full CWP integration configuration page.
 *
 * Renders CwpBrandHeader, IntegrationConfigForm, and CwpStatusSidebar in a two-column layout.
 * Fetches config and server-info in parallel on mount.
 */
import { computed, onMounted, ref } from 'vue'
import { useIntegrationsStore } from '../stores/integrationsStore'
import { useCwpServerInfo } from '../composables/useCwpServerInfo'
import CwpBrandHeader from '../components/CwpBrandHeader.vue'
import CwpStatusSidebar from '../components/CwpStatusSidebar.vue'
import IntegrationConfigForm from '../components/IntegrationConfigForm.vue'

const store = useIntegrationsStore()
const { info, loading: infoLoading, fetch: fetchInfo } = useCwpServerInfo()

/** True while the test connection request is in progress. */
const testing = ref(false)

/** Error message from failed test, shown under the form. */
const testError = ref<string | null>(null)

/** Derived header status from server-info. */
const headerStatus = computed(() => {
  if (!info.value) return 'unknown' as const
  return info.value.connected ? 'connected' as const : 'error' as const
})

onMounted(async () => {
  await Promise.all([
    store.fetchOne('cwp'),
    fetchInfo(),
  ])
})

/**
 * Saves the current config to the backend, then re-fetches server-info.
 *
 * @returns Promise resolving when save and re-fetch complete.
 */
async function handleSave(): Promise<void> {
  await store.saveConfig('cwp', {
    isEnabled: store.current?.isEnabled ?? false,
    config: store.current?.config ?? {},
  })
  await fetchInfo()
}

/**
 * Tests the CWP connection, updates sidebar, invalidates server-info cache.
 *
 * @returns Promise resolving when test completes.
 */
async function handleTest(): Promise<void> {
  testing.value = true
  testError.value = null
  try {
    const result = await store.testConnection('cwp')
    if (!result.success) testError.value = result.message
    await fetchInfo()
  } finally {
    testing.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Branded header -->
    <CwpBrandHeader
      :version="info?.cwpVersion ?? null"
      :status="headerStatus"
    />

    <!-- Two-column layout -->
    <div class="grid grid-cols-1 gap-6 lg:grid-cols-3">
      <!-- Config form — 2/3 width -->
      <div class="lg:col-span-2 space-y-4">
        <IntegrationConfigForm
          v-if="store.current"
          :integration="store.current"
          @save="handleSave"
        />

        <!-- Test error banner -->
        <div
          v-if="testError"
          class="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"
        >
          Could not connect: {{ testError }}
        </div>
      </div>

      <!-- Status sidebar — 1/3 width -->
      <div>
        <CwpStatusSidebar
          :info="info"
          :testing="testing"
          @test="handleTest"
        />
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/views/CwpIntegrationPage.vue
git commit -m "feat(cwp): add CwpIntegrationPage view"
```

---

## Task 12: Update `IntegrationDetailView` to route to CwpIntegrationPage

**Files:**
- Modify: `admin/src/modules/integrations/views/IntegrationDetailView.vue`

- [ ] **Step 1: Add slug detection and redirect logic**

In `IntegrationDetailView.vue`, locate the `onMounted` / `setup` block and add:

```typescript
import { defineAsyncComponent } from 'vue'
import { useRouter } from 'vue-router'
import { useIntegrationsStore } from '../stores/integrationsStore'

const route = useRoute()
const router = useRouter()
const store = useIntegrationsStore()

const slug = route.params.slug as string
const isCwp = slug === 'cwp'

// If CWP slug but plugin not installed, redirect to Plugin Manager
if (isCwp && !store.integrations.some(i => i.slug === 'cwp')) {
  router.replace({ path: '/plugins', query: { toast: 'Install the CWP plugin first.' } })
}

const CwpIntegrationPage = defineAsyncComponent(
  () => import('./CwpIntegrationPage.vue')
)
```

In the template, wrap the existing content:

```vue
<template>
  <CwpIntegrationPage v-if="isCwp" />
  <div v-else>
    <!-- existing generic integration detail content -->
  </div>
</template>
```

- [ ] **Step 2: Build TypeScript check**

```bash
cd admin
yarn vue-tsc --noEmit
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add admin/src/modules/integrations/views/IntegrationDetailView.vue
git commit -m "feat(integrations): route cwp slug to CwpIntegrationPage"
```

---

## Task 13: Show plugin badge in integrations grid

**Files:**
- Modify: `admin/src/modules/integrations/stores/integrationsStore.ts`

The `isPlugin` field is already propagated from the backend (Task 3). The grid component reads `IntegrationDto` — just verify the card template shows the badge.

- [ ] **Step 1: Find the integration card/row component**

Search for the component rendering the integrations grid item (likely `IntegrationRow.vue` or `IntegrationSection.vue`).

- [ ] **Step 2: Add plugin badge**

In the integration card template, add after the integration name:

```vue
<span
  v-if="integration.isPlugin"
  class="ml-2 rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700"
>
  Plugin
</span>
```

- [ ] **Step 3: Commit**

```bash
git add admin/src/modules/integrations/components/
git commit -m "feat(integrations): show Plugin badge on plugin-sourced integration cards"
```

---

## Task 14: Final build verification

- [ ] **Step 1: Build backend**

```bash
cd backend
dotnet build
```

Expected: 0 errors, 0 warnings (except nullable).

- [ ] **Step 2: Run all backend tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 3: Run dotnet format check**

```bash
dotnet format --verify-no-changes
```

Expected: No formatting issues.

- [ ] **Step 4: Build frontend**

```bash
cd admin
yarn build
```

Expected: Build succeeded, 0 TypeScript errors.

- [ ] **Step 5: Final commit if any fixes needed**

```bash
git add -A
git commit -m "fix: final build cleanup for CWP integration"
```
