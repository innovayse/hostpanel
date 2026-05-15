# Plugin System — Plan C: Admin Panel UI + Field Definitions API

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Vue 3 Plugin Manager page (upload/list/remove/restart), wire `/plugins` into the router and sidebar, and fix the backend `IntegrationDetailDto` to return API-driven field definitions so `IntegrationConfigForm.vue` no longer needs the static `INTEGRATION_META`.

**Architecture:** Backend gains a `FieldDefinitionDto` POCO and `GetIntegrationHandler` is refactored to return field definitions alongside config values. `SaveIntegrationConfigRequest.Fields` is renamed to `Config` to match what the frontend already sends. Frontend: new `modules/plugins/` Pinia store + four Vue components + `PluginsView` assembled from them. `IntegrationConfigForm.vue` drops the static `INTEGRATION_META` import and reads `props.integration.fieldDefinitions` instead.

**Tech Stack:** ASP.NET Core 8 (C#), Vue 3 Composition API `<script setup lang="ts">`, Pinia, Tailwind CSS, Vite proxy (`/api` → `http://localhost:5148`)

**Pre-existing bug fixed in Task 1:** Backend returned `fields` (from `IntegrationDetailDto.Fields`) but frontend TypeScript expected `config` — integration config page showed empty values. Rename `Fields` → `Config` throughout.

---

## File Map

```
Modified — Backend
  backend/src/Innovayse.Application/Admin/Integrations/DTOs/FieldDefinitionDto.cs         (NEW)
  backend/src/Innovayse.Application/Admin/Integrations/DTOs/IntegrationDetailDto.cs        rename Fields→Config, add FieldDefinitions
  backend/src/Innovayse.Application/Admin/Integrations/Queries/GetIntegration/
    GetIntegrationHandler.cs                                                                  refactor _meta, return FieldDefinitions
  backend/src/Innovayse.Application/Admin/Integrations/Commands/SaveIntegrationConfig/
    SaveIntegrationConfigCommand.cs                                                            rename Fields→Config
    SaveIntegrationConfigHandler.cs                                                            rename command.Fields→command.Config
  backend/src/Innovayse.API/Admin/Requests/SaveIntegrationConfigRequest.cs                  rename Fields→Config

Modified — Frontend integration module
  admin/src/modules/integrations/types/integration.types.ts                                 add FieldDefinitionDto, update IntegrationDetailDto
  admin/src/modules/integrations/components/IntegrationConfigForm.vue                       use fieldDefinitions from API

New — Frontend plugins module
  admin/src/modules/plugins/types/plugin.types.ts
  admin/src/modules/plugins/stores/pluginsStore.ts
  admin/src/modules/plugins/components/PluginCard.vue
  admin/src/modules/plugins/components/RestartBanner.vue
  admin/src/modules/plugins/components/PluginUploader.vue
  admin/src/modules/plugins/views/PluginsView.vue

Modified — Router + Sidebar
  admin/src/router/index.ts                                                                   add /plugins route
  admin/src/components/layout/AppSidebar.vue                                                 add Plugin Manager nav item
```

---

### Task 1: Backend — fix `Fields→Config` rename + add `FieldDefinitions` to integration detail

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Integrations/DTOs/FieldDefinitionDto.cs`
- Modify: `backend/src/Innovayse.Application/Admin/Integrations/DTOs/IntegrationDetailDto.cs`
- Modify: `backend/src/Innovayse.Application/Admin/Integrations/Queries/GetIntegration/GetIntegrationHandler.cs`
- Modify: `backend/src/Innovayse.Application/Admin/Integrations/Commands/SaveIntegrationConfig/SaveIntegrationConfigCommand.cs`
- Modify: `backend/src/Innovayse.Application/Admin/Integrations/Commands/SaveIntegrationConfig/SaveIntegrationConfigHandler.cs`
- Modify: `backend/src/Innovayse.API/Admin/Requests/SaveIntegrationConfigRequest.cs`

- [ ] **Step 1: Create `FieldDefinitionDto.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Describes one configuration field for an integration or plugin.
/// Returned by the detail endpoint so the admin panel can render the form dynamically.
/// </summary>
/// <param name="Key">Storage key used in the Settings table (e.g. "secret_key").</param>
/// <param name="Label">Human-readable label shown above the field (e.g. "Secret Key").</param>
/// <param name="Type">Input type — "text", "password", "select", or "textarea".</param>
/// <param name="Required">Whether the field must be non-empty before the integration can be enabled.</param>
/// <param name="Options">Allowed values for "select" type fields; null for all other types.</param>
public record FieldDefinitionDto(
    string Key,
    string Label,
    string Type,
    bool Required,
    IReadOnlyList<string>? Options = null);
```

- [ ] **Step 2: Rewrite `IntegrationDetailDto.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Full configuration for one integration, with secret fields masked.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration.</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="Config">
/// Dictionary mapping field key (e.g. "secret_key") to its stored value.
/// Any field whose key contains "key", "secret", "password", or "token" is
/// returned as "••••••••" when non-empty, or "" when empty.
/// </param>
/// <param name="FieldDefinitions">
/// Ordered list of field metadata used by the admin panel to render the config form.
/// </param>
public record IntegrationDetailDto(
    string Slug,
    bool IsEnabled,
    Dictionary<string, string> Config,
    IReadOnlyList<FieldDefinitionDto> FieldDefinitions);
```

- [ ] **Step 3: Rewrite `GetIntegrationHandler.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Queries.GetIntegration;

using Innovayse.Application.Admin.Integrations.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="GetIntegrationQuery"/> by loading all settings for the given slug
/// and returning them with secret values masked alongside field definitions.
/// </summary>
/// <param name="settings">Setting repository for key-value lookups.</param>
public sealed class GetIntegrationHandler(ISettingRepository settings)
{
    /// <summary>
    /// Substring patterns that identify a field as a secret.
    /// Any field key containing one of these strings (case-insensitive) will be masked.
    /// </summary>
    private static readonly string[] _secretMarkers = ["key", "secret", "password", "token"];

    /// <summary>
    /// Static metadata for every built-in integration.
    /// Tuple: (display name, category label, ordered field definitions).
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Category, IReadOnlyList<FieldDefinitionDto> Fields)> _meta = new()
    {
        ["stripe"] = ("Stripe", "Payment Gateways",
        [
            new("secret_key",      "Secret Key",      "password", Required: true),
            new("publishable_key", "Publishable Key", "text",     Required: true),
            new("webhook_secret",  "Webhook Secret",  "password", Required: false),
            new("mode",            "Mode",            "select",   Required: false, Options: ["Live", "Test"]),
        ]),
        ["paypal"] = ("PayPal", "Payment Gateways",
        [
            new("client_id",     "Client ID",     "text",     Required: true),
            new("client_secret", "Client Secret", "password", Required: true),
            new("mode",          "Mode",          "select",   Required: false, Options: ["Live", "Sandbox"]),
        ]),
        ["bank-transfer"] = ("Bank Transfer", "Payment Gateways",
        [
            new("account_name", "Account Name",       "text",     Required: false),
            new("iban",         "IBAN",               "text",     Required: false),
            new("bank_name",    "Bank Name",          "text",     Required: false),
            new("instructions", "Payment Instructions","textarea", Required: false),
        ]),
        ["namecheap"] = ("Namecheap", "Domain Registrars",
        [
            new("api_key",      "API Key",                "password", Required: true),
            new("api_username", "API Username",           "text",     Required: true),
            new("client_ip",    "Whitelisted Client IP",  "text",     Required: true),
        ]),
        ["resellerclub"] = ("ResellerClub", "Domain Registrars",
        [
            new("reseller_id", "Reseller ID", "text",     Required: true),
            new("api_key",     "API Key",     "password", Required: true),
        ]),
        ["enom"] = ("ENOM", "Domain Registrars",
        [
            new("account_id", "Account ID", "text",     Required: true),
            new("api_key",    "API Key",    "password", Required: true),
        ]),
        ["cpanel"] = ("cPanel WHM", "Hosting / Provisioning",
        [
            new("host",      "WHM Host",  "text",     Required: true),
            new("port",      "Port",      "text",     Required: false),
            new("username",  "Username",  "text",     Required: true),
            new("api_token", "API Token", "password", Required: true),
        ]),
        ["plesk"] = ("Plesk", "Hosting / Provisioning",
        [
            new("host",     "Plesk Host", "text",     Required: true),
            new("port",     "Port",       "text",     Required: false),
            new("username", "Username",   "text",     Required: true),
            new("password", "Password",   "password", Required: true),
        ]),
        ["smtp"] = ("SMTP Server", "Email / SMTP",
        [
            new("host",         "SMTP Host",    "text",     Required: true),
            new("port",         "Port",         "text",     Required: false),
            new("username",     "Username",     "text",     Required: false),
            new("password",     "Password",     "password", Required: false),
            new("from_address", "From Address", "text",     Required: true),
            new("encryption",   "Encryption",   "select",   Required: false, Options: ["TLS", "SSL", "None"]),
        ]),
        ["maxmind"] = ("MaxMind", "Fraud Protection",
        [
            new("account_id",  "Account ID",  "text",     Required: true),
            new("license_key", "License Key", "password", Required: true),
        ]),
    };

    /// <summary>
    /// Loads the configuration for the requested integration, masks secret fields,
    /// and returns field definitions for dynamic form rendering.
    /// </summary>
    /// <param name="query">Query containing the integration slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The integration detail DTO with masked config and field definitions,
    /// or null if the slug is not recognised.
    /// </returns>
    public async Task<IntegrationDetailDto?> HandleAsync(GetIntegrationQuery query, CancellationToken ct)
    {
        if (!_meta.TryGetValue(query.Slug, out var meta))
            return null;

        var all = await settings.ListAsync(ct);
        var prefix = $"integration:{query.Slug}:";

        var lookup = all
            .Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var isEnabled = lookup.TryGetValue($"{prefix}is_enabled", out var enabledVal)
            && string.Equals(enabledVal, "true", StringComparison.OrdinalIgnoreCase);

        var config = new Dictionary<string, string>(meta.Fields.Count);
        foreach (var field in meta.Fields)
        {
            lookup.TryGetValue($"{prefix}{field.Key}", out var raw);
            config[field.Key] = MaskIfSecret(field.Key, raw ?? string.Empty);
        }

        return new IntegrationDetailDto(query.Slug, isEnabled, config, meta.Fields);
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

        var isSecret = _secretMarkers.Any(m => fieldKey.Contains(m, StringComparison.OrdinalIgnoreCase));
        return isSecret ? "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022" : value;
    }
}
```

- [ ] **Step 4: Rename `Fields` → `Config` in `SaveIntegrationConfigCommand.cs`**

```csharp
namespace Innovayse.Application.Admin.Integrations.Commands.SaveIntegrationConfig;

/// <summary>
/// Command that upserts all configuration settings for one integration.
/// </summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "stripe".</param>
/// <param name="IsEnabled">Whether the integration should be active after saving.</param>
/// <param name="Config">
/// Dictionary of field key to value to persist.
/// Fields whose value equals the mask placeholder ("••••••••") are skipped so stored
/// secrets are not overwritten when the admin re-saves without changing them.
/// </param>
public record SaveIntegrationConfigCommand(
    string Slug,
    bool IsEnabled,
    Dictionary<string, string> Config);
```

- [ ] **Step 5: Update `SaveIntegrationConfigHandler.cs` — rename `command.Fields` → `command.Config`**

Read the file first, then find every occurrence of `command.Fields` and replace with `command.Config`. Also update the XML doc `<param>` tag for `Fields` to `Config`.

- [ ] **Step 6: Rename `Fields` → `Config` in `SaveIntegrationConfigRequest.cs`**

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
    public required Dictionary<string, string> Config { get; init; }
}
```

- [ ] **Step 7: Update `IntegrationsController.SaveAsync` call**

In `backend/src/Innovayse.API/Admin/IntegrationsController.cs`, find:
```csharp
new SaveIntegrationConfigCommand(slug, req.IsEnabled, req.Fields)
```
Change to:
```csharp
new SaveIntegrationConfigCommand(slug, req.IsEnabled, req.Config)
```

- [ ] **Step 8: Build full solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build
```

Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Application/Admin/Integrations/
git add backend/src/Innovayse.API/Admin/
git commit -m "fix(integrations): rename Fields→Config in DTO/command/request, add FieldDefinitions to detail endpoint"
```

---

### Task 2: Frontend types — `FieldDefinitionDto` + update `IntegrationDetailDto` + `plugin.types.ts`

**Files:**
- Modify: `admin/src/modules/integrations/types/integration.types.ts`
- Create: `admin/src/modules/plugins/types/plugin.types.ts`

- [ ] **Step 1: Update `integration.types.ts`**

Add `FieldDefinitionDto` interface and add `fieldDefinitions` to `IntegrationDetailDto`. Replace the full file:

```typescript
/** Category grouping for an integration. */
export type IntegrationCategory =
  | 'payments'
  | 'registrars'
  | 'provisioning'
  | 'email'
  | 'fraud'

/** All valid integration slugs. */
export type IntegrationSlug =
  | 'stripe'
  | 'paypal'
  | 'bank-transfer'
  | 'namecheap'
  | 'resellerclub'
  | 'enom'
  | 'cpanel'
  | 'plesk'
  | 'smtp'
  | 'maxmind'

/** Summary of a single integration returned by GET /api/admin/integrations. */
export interface IntegrationDto {
  /** URL-safe slug used in routes and API calls (e.g. "stripe", "cpanel"). */
  slug: string
  /** Human-readable name (e.g. "Stripe"). */
  name: string
  /** Short description shown in the integration row. */
  description: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** Whether this integration is currently enabled. */
  isEnabled: boolean
  /** ISO 8601 timestamp of the last successful connection test, or null. */
  lastTestedAt: string | null
}

/** Metadata for one configuration field, returned by the detail endpoint. */
export interface FieldDefinitionDto {
  /** Storage key used in the Settings table (e.g. "secret_key"). */
  key: string
  /** Human-readable label shown above the input. */
  label: string
  /** Input type — "text", "password", "select", or "textarea". */
  type: 'text' | 'password' | 'select' | 'textarea'
  /** Whether the field is required before the integration can be enabled. */
  required: boolean
  /** Allowed values for "select" type fields; absent for all others. */
  options?: string[]
}

/** Full config for a single integration returned by GET /api/admin/integrations/:slug. */
export interface IntegrationDetailDto extends IntegrationDto {
  /**
   * Key-value pairs of configuration fields.
   * Secret values are masked (e.g. "sk_live_••••••••").
   */
  config: Record<string, string>
  /**
   * Ordered field definitions for dynamic form rendering.
   * Source of truth for which fields to show and their input types.
   */
  fieldDefinitions: FieldDefinitionDto[]
}

/** Payload sent to PUT /api/admin/integrations/:slug. */
export interface IntegrationConfigPayload {
  /** Whether the integration should be enabled after save. */
  isEnabled: boolean
  /** Full (unmasked) config values the admin has edited. */
  config: Record<string, string>
}

/** Result returned by POST /api/admin/integrations/:slug/test. */
export interface IntegrationTestResult {
  /** Whether the connection test succeeded. */
  success: boolean
  /** Human-readable message (error detail or "Connection OK"). */
  message: string
}

/**
 * Static metadata for rendering each integration card.
 * Not fetched from the API — defined client-side.
 */
export interface IntegrationMeta {
  /** Matches IntegrationDto.slug. */
  slug: string
  /** Tailwind background color class for the logo block (e.g. "bg-[#635bff]"). */
  color: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** Ordered list of config field keys and their labels. */
  fields: IntegrationField[]
  /** Optional hint shown in the status sidebar (e.g. webhook URL instructions). */
  hint?: string
}

/** A single configurable field for an integration. */
export interface IntegrationField {
  /** Field key matching the config Record key. */
  key: string
  /** Human-readable label. */
  label: string
  /** Input type — "text" for most, "password" for secrets, "select" for enums. */
  type: 'text' | 'password' | 'select' | 'textarea'
  /** Options for "select" type fields. */
  options?: string[]
}
```

- [ ] **Step 2: Create `admin/src/modules/plugins/types/plugin.types.ts`**

```typescript
/** Summary of one installed plugin returned by GET /api/admin/plugins. */
export interface PluginListItemDto {
  /** Unique plugin identifier from plugin.json (e.g. "innovayse-cwp"). */
  id: string
  /** Human-readable display name. */
  name: string
  /** Semver version string (e.g. "1.0.0"). */
  version: string
  /** Author name or organisation. */
  author: string
  /** Short description shown in the plugin card. */
  description: string
  /** Plugin type: "provisioning" | "payment" | "registrar". */
  type: string
  /** Display category label (e.g. "Hosting / Provisioning"). */
  category: string
  /** Hex colour for the plugin logo block (e.g. "#1a73e8"). */
  color: string
  /** Whether the DLL was successfully loaded at the last startup. */
  isLoaded: boolean
}

/** Result returned by install and remove plugin operations. */
export interface PluginActionResultDto {
  /** Always true — a server restart is required for changes to take effect. */
  requiresRestart: boolean
}
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add admin/src/modules/integrations/types/integration.types.ts
git add admin/src/modules/plugins/
git commit -m "feat(plugins): add FieldDefinitionDto to integration types, add plugin.types.ts"
```

---

### Task 3: `pluginsStore.ts`

**Files:**
- Create: `admin/src/modules/plugins/stores/pluginsStore.ts`

- [ ] **Step 1: Create `pluginsStore.ts`**

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { PluginListItemDto, PluginActionResultDto } from '../types/plugin.types'

/**
 * Pinia store for managing installed plugins.
 *
 * Handles list, install (multipart ZIP), remove, and restart operations.
 */
export const usePluginsStore = defineStore('plugins', () => {
  const { request } = useApi()

  /** Installed plugin list, populated by {@link fetchAll}. */
  const plugins = ref<PluginListItemDto[]>([])

  /** True while any request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Whether a server restart is required for pending plugin changes to take effect.
   * Set to true after a successful install or remove, cleared after restart.
   */
  const requiresRestart = ref(false)

  /**
   * Fetches the list of all installed plugins.
   *
   * @returns Promise that resolves when the list is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      plugins.value = await request<PluginListItemDto[]>('/admin/plugins')
    } catch {
      error.value = 'Failed to load plugins.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Uploads and installs a plugin from a ZIP file.
   *
   * @param file - The ZIP file selected by the admin.
   * @returns Promise that resolves when the install request completes.
   */
  async function install(file: File): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const form = new FormData()
      form.append('file', file)

      const token = localStorage.getItem('accessToken') ?? ''
      const res = await fetch('/api/admin/plugins/install', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
        body: form,
      })

      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || `HTTP ${res.status}`)
      }

      const result: PluginActionResultDto = await res.json()
      requiresRestart.value = result.requiresRestart
      await fetchAll()
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to install plugin.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Removes an installed plugin by its identifier.
   *
   * @param id - Plugin identifier as declared in plugin.json.
   * @returns Promise that resolves when the remove request completes.
   */
  async function remove(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PluginActionResultDto>(`/admin/plugins/${id}`, {
        method: 'DELETE',
      })
      requiresRestart.value = result.requiresRestart
      plugins.value = plugins.value.filter(p => p.id !== id)
    } catch {
      error.value = 'Failed to remove plugin.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Triggers a graceful server restart so installed/removed plugins take effect.
   * After calling this, poll /api/health until the server responds.
   *
   * @returns Promise that resolves when the restart request is sent.
   */
  async function restart(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request('/admin/plugins/restart', { method: 'POST' })
      requiresRestart.value = false
    } catch {
      // Server may disconnect immediately — that's expected behaviour
      requiresRestart.value = false
    } finally {
      loading.value = false
    }
  }

  return { plugins, loading, error, requiresRestart, fetchAll, install, remove, restart }
})
```

- [ ] **Step 2: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add admin/src/modules/plugins/stores/pluginsStore.ts
git commit -m "feat(plugins): add pluginsStore — fetch, install, remove, restart"
```

---

### Task 4: `PluginCard.vue` + `RestartBanner.vue`

**Files:**
- Create: `admin/src/modules/plugins/components/PluginCard.vue`
- Create: `admin/src/modules/plugins/components/RestartBanner.vue`

- [ ] **Step 1: Create `PluginCard.vue`**

```vue
<script setup lang="ts">
/**
 * Displays a single installed plugin with its status and a remove button.
 */
import type { PluginListItemDto } from '../types/plugin.types'

/** Props for PluginCard. */
const props = defineProps<{
  /** The plugin to display. */
  plugin: PluginListItemDto
  /** True while a remove request is in flight for this plugin. */
  removing: boolean
}>()

/** Emits for PluginCard. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Remove. */
  remove: [id: string]
}>()
</script>

<template>
  <div class="flex items-start gap-4 p-4 bg-white rounded-xl border border-gray-200">
    <!-- Color block -->
    <div
      class="w-10 h-10 rounded-lg flex-shrink-0"
      :style="{ backgroundColor: plugin.color }"
    />

    <!-- Info -->
    <div class="flex-1 min-w-0">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-gray-800 text-sm">{{ plugin.name }}</span>
        <span class="text-xs text-gray-400">v{{ plugin.version }}</span>
        <span
          class="text-xs font-medium px-2 py-0.5 rounded-full"
          :class="plugin.isLoaded ? 'bg-green-100 text-green-700' : 'bg-yellow-100 text-yellow-700'"
        >
          {{ plugin.isLoaded ? 'Loaded' : 'Pending restart' }}
        </span>
      </div>
      <div class="text-xs text-gray-500 mt-0.5">{{ plugin.description }}</div>
      <div class="text-xs text-gray-400 mt-1">{{ plugin.category }} · by {{ plugin.author }}</div>
    </div>

    <!-- Remove -->
    <button
      type="button"
      class="text-xs text-red-600 hover:text-red-800 font-medium transition disabled:opacity-40"
      :disabled="removing"
      @click="emit('remove', plugin.id)"
    >
      Remove
    </button>
  </div>
</template>
```

- [ ] **Step 2: Create `RestartBanner.vue`**

```vue
<script setup lang="ts">
/**
 * Warning banner displayed after install or remove operations.
 * Prompts the admin to restart the server for changes to take effect.
 */

/** Emits for RestartBanner. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Restart Server. */
  restart: []
}>()

/** Props for RestartBanner. */
defineProps<{
  /** True while the restart request is in flight. */
  restarting: boolean
}>()
</script>

<template>
  <div class="flex items-center gap-4 bg-amber-50 border border-amber-200 rounded-xl px-5 py-3">
    <span class="text-amber-600 text-sm font-medium flex-1">
      ⚠ Restart required to apply plugin changes.
    </span>
    <button
      type="button"
      class="bg-amber-500 hover:bg-amber-600 text-white text-xs font-semibold px-4 py-1.5 rounded-lg transition disabled:opacity-50"
      :disabled="restarting"
      @click="emit('restart')"
    >
      {{ restarting ? 'Restarting…' : 'Restart Server' }}
    </button>
  </div>
</template>
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add admin/src/modules/plugins/components/
git commit -m "feat(plugins): add PluginCard and RestartBanner components"
```

---

### Task 5: `PluginUploader.vue`

**Files:**
- Create: `admin/src/modules/plugins/components/PluginUploader.vue`

- [ ] **Step 1: Create `PluginUploader.vue`**

```vue
<script setup lang="ts">
/**
 * File upload dropzone for installing a plugin ZIP.
 * Validates that the selected file has a .zip extension before emitting.
 */
import { ref } from 'vue'

/** Emits for PluginUploader. */
const emit = defineEmits<{
  /** Emitted with the selected ZIP file when the admin confirms the upload. */
  upload: [file: File]
}>()

/** The file the admin has selected (before confirming). */
const selectedFile = ref<File | null>(null)

/** Validation error shown when a non-ZIP file is selected. */
const validationError = ref<string | null>(null)

/**
 * Handles file input change — validates extension and stages the file.
 *
 * @param event - The native change event from the file input.
 */
function handleFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  validationError.value = null
  selectedFile.value = null

  if (!file) return

  if (!file.name.endsWith('.zip')) {
    validationError.value = 'Only .zip files are accepted.'
    return
  }

  selectedFile.value = file
}

/**
 * Emits the staged file and resets local state.
 */
function handleUpload(): void {
  if (!selectedFile.value) return
  emit('upload', selectedFile.value)
  selectedFile.value = null
}
</script>

<template>
  <div class="bg-white rounded-xl border border-dashed border-gray-300 p-6 text-center">
    <p class="text-sm text-gray-500 mb-3">Upload a plugin <span class="font-mono">.zip</span> file</p>

    <input
      id="plugin-zip-input"
      type="file"
      accept=".zip"
      class="hidden"
      @change="handleFileChange"
    />

    <label
      for="plugin-zip-input"
      class="cursor-pointer inline-block bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-medium px-4 py-2 rounded-lg transition"
    >
      Choose file
    </label>

    <div v-if="selectedFile" class="mt-3 text-sm text-gray-700">
      Selected: <span class="font-mono">{{ selectedFile.name }}</span>
      <button
        type="button"
        class="ml-3 bg-blue-700 hover:bg-blue-800 text-white text-xs font-semibold px-4 py-1.5 rounded-lg transition"
        @click="handleUpload"
      >
        Install Plugin
      </button>
    </div>

    <p v-if="validationError" class="mt-2 text-xs text-red-600">{{ validationError }}</p>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add admin/src/modules/plugins/components/PluginUploader.vue
git commit -m "feat(plugins): add PluginUploader component"
```

---

### Task 6: `PluginsView.vue`

**Files:**
- Create: `admin/src/modules/plugins/views/PluginsView.vue`

- [ ] **Step 1: Create `PluginsView.vue`**

```vue
<script setup lang="ts">
/**
 * Plugin Manager page.
 *
 * Lists installed plugins, provides ZIP upload for installation,
 * and shows a restart banner when pending changes need to take effect.
 */
import { ref, onMounted } from 'vue'
import { usePluginsStore } from '../stores/pluginsStore'
import PluginCard from '../components/PluginCard.vue'
import PluginUploader from '../components/PluginUploader.vue'
import RestartBanner from '../components/RestartBanner.vue'

const store = usePluginsStore()

/** ID of the plugin currently being removed (for per-card loading state). */
const removingId = ref<string | null>(null)

/** True while a restart request is in flight. */
const restarting = ref(false)

onMounted(store.fetchAll)

/**
 * Removes a plugin and sets the per-card loading indicator.
 *
 * @param id - Plugin identifier to remove.
 */
async function handleRemove(id: string): Promise<void> {
  removingId.value = id
  await store.remove(id)
  removingId.value = null
}

/**
 * Triggers a graceful server restart and shows a restarting indicator.
 */
async function handleRestart(): Promise<void> {
  restarting.value = true
  await store.restart()
  restarting.value = false
}

/**
 * Installs the given ZIP file via the store.
 *
 * @param file - ZIP file selected in PluginUploader.
 */
async function handleUpload(file: File): Promise<void> {
  await store.install(file)
}
</script>

<template>
  <div class="max-w-3xl">
    <!-- Page header -->
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-gray-800">Plugin Manager</h1>
      <p class="text-sm text-gray-500 mt-1">Install and manage provider plugins</p>
    </div>

    <!-- Restart banner -->
    <RestartBanner
      v-if="store.requiresRestart"
      :restarting="restarting"
      class="mb-5"
      @restart="handleRestart"
    />

    <!-- Upload -->
    <PluginUploader
      class="mb-6"
      @upload="handleUpload"
    />

    <!-- Error -->
    <div v-if="store.error" class="mb-4 text-sm text-red-600">{{ store.error }}</div>

    <!-- Loading -->
    <div v-if="store.loading && store.plugins.length === 0" class="text-gray-400 text-sm">
      Loading plugins…
    </div>

    <!-- Empty state -->
    <div
      v-else-if="!store.loading && store.plugins.length === 0"
      class="text-sm text-gray-400 bg-white rounded-xl border border-gray-200 p-8 text-center"
    >
      No plugins installed yet. Upload a <span class="font-mono">.zip</span> file above to get started.
    </div>

    <!-- Plugin list -->
    <div v-else class="flex flex-col gap-3">
      <h2 class="text-sm font-semibold text-gray-600 uppercase tracking-wide">Installed Plugins</h2>
      <PluginCard
        v-for="plugin in store.plugins"
        :key="plugin.id"
        :plugin="plugin"
        :removing="removingId === plugin.id"
        @remove="handleRemove"
      />
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add admin/src/modules/plugins/views/PluginsView.vue
git commit -m "feat(plugins): add PluginsView — list, upload, remove, restart"
```

---

### Task 7: Router + Sidebar + `IntegrationConfigForm.vue` update

**Files:**
- Modify: `admin/src/router/index.ts`
- Modify: `admin/src/components/layout/AppSidebar.vue`
- Modify: `admin/src/modules/integrations/components/IntegrationConfigForm.vue`

- [ ] **Step 1: Add `/plugins` route to `router/index.ts`**

Inside the children array (after the `integrations/:slug` entry, before `settings`), add:

```typescript
{ path: 'plugins', component: () => import('../modules/plugins/views/PluginsView.vue') },
```

- [ ] **Step 2: Add "Plugin Manager" to `AppSidebar.vue` nav items**

In the `navItems` array, add `{ to: '/plugins', label: 'Plugin Manager' }` **before** the `Apps & Integrations` item:

```typescript
const navItems = [
  { to: '/dashboard', label: 'Dashboard' },
  { to: '/clients', label: 'Clients' },
  { to: '/billing', label: 'Billing' },
  { to: '/services', label: 'Services' },
  { to: '/domains', label: 'Domains' },
  { to: '/support', label: 'Support' },
  { to: '/plugins', label: 'Plugin Manager' },
  { to: '/integrations', label: 'Apps & Integrations' },
  { to: '/settings', label: 'Settings' },
  { to: '/settings/products', label: '  Products' },
  { to: '/settings/email-templates', label: '  Email Templates' },
  { to: '/settings/gateways', label: '  Gateways' },
]
```

- [ ] **Step 3: Update `IntegrationConfigForm.vue` — use `fieldDefinitions` from API**

Replace the full `<script setup>` section of `IntegrationConfigForm.vue`:

```vue
<script setup lang="ts">
/**
 * Dynamic config form for a single integration.
 *
 * Renders fields from integration.fieldDefinitions (API-driven).
 * Emits save and test events to the parent (IntegrationDetailView).
 */
import { ref, watch } from 'vue'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationDetailDto, IntegrationConfigPayload } from '../types/integration.types'

/** Props for IntegrationConfigForm. */
const props = defineProps<{
  /** Full integration detail loaded from the store. */
  integration: IntegrationDetailDto
  /** True while a save or test request is in flight. */
  loading: boolean
}>()

/** Emits for IntegrationConfigForm. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Save Changes. */
  save: [payload: IntegrationConfigPayload]
  /** Emitted when the admin clicks Test Connection. */
  test: []
}>()

/** Color and hint metadata for this integration (still client-side for color/hint only). */
const meta = INTEGRATION_META[props.integration.slug as keyof typeof INTEGRATION_META]

/** Local copy of config values the admin is editing. */
const localConfig = ref<Record<string, string>>({ ...props.integration.config })

/** Local toggle for enabled/disabled state. */
const isEnabled = ref(props.integration.isEnabled)

/** Re-sync local state when a new integration is loaded. */
watch(
  () => props.integration,
  (next) => {
    localConfig.value = { ...next.config }
    isEnabled.value = next.isEnabled
  },
  { deep: true }
)

/**
 * Builds and emits the save payload.
 */
function handleSave(): void {
  emit('save', { isEnabled: isEnabled.value, config: { ...localConfig.value } })
}
</script>
```

Also update the `<template>` to use `integration.fieldDefinitions` instead of `meta?.fields ?? []`:

Find:
```html
<div
  v-for="field in meta?.fields ?? []"
  :key="field.key"
```

Replace with:
```html
<div
  v-for="field in integration.fieldDefinitions ?? []"
  :key="field.key"
```

Keep the rest of the template identical. The color block still uses `meta?.color`.

- [ ] **Step 4: Start the admin dev server and verify**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
./scripts/manage.sh start admin
sleep 3
# Open http://localhost:5173/plugins in browser
# Should show Plugin Manager page with uploader and empty state
# Open http://localhost:5173/integrations/stripe
# Should show Stripe config form with fields from API (after backend is running)
```

Expected: no console errors, Plugin Manager page visible, Stripe form renders.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add admin/src/router/index.ts
git add admin/src/components/layout/AppSidebar.vue
git add admin/src/modules/integrations/components/IntegrationConfigForm.vue
git commit -m "feat(plugins): add /plugins route, sidebar nav, update ConfigForm to use API field definitions"
```
