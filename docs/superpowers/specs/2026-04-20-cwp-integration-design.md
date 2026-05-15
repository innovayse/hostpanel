# CWP Apps & Integrations — Design Spec
**Date:** 2026-04-20  
**Status:** Approved

---

## Overview

CWP (CentOS Web Panel) provisioning plugin auto-appears in the Apps & Integrations UI after installation via Plugin Manager. The config page features a dark branded header, dynamic config form, and a live status sidebar showing connection health, accounts count, and CWP version.

**Approach:** Plugin-first with override mechanism — `plugin.json` declares `showInIntegrations: true`, backend merges plugin into integrations list, frontend renders plugin-specific page component when slug matches.

---

## Section 1: Architecture

### Backend Changes

#### 1. `plugin.json` — new fields
```json
{
  "showInIntegrations": true,
  "integrationsCategory": "Provisioning"
}
```

#### 2. `PluginLoader.cs`
During manifest load, plugins with `showInIntegrations: true` are registered into the `IntegrationsController` data source alongside built-in integrations.

#### 3. New endpoint — `GET /api/admin/integrations/cwp/server-info`
Returns CWP server status fetched from CWP API, cached 5 minutes via memory cache.

```json
{
  "connected": true,
  "accountsCount": 142,
  "cwpVersion": "1.9.1",
  "lastTestedAt": "2026-04-20T10:30:00Z"
}
```

**Handler location:** `Application/Admin/Integrations/Queries/GetCwpServerInfo/`  
**Cache key:** `"cwp:server-info"`  
**Cache TTL:** 5 minutes (invalidated on Test Connection)

#### 4. Existing endpoints (no changes needed)
- `GET /api/admin/integrations/cwp` — config + field definitions
- `PUT /api/admin/integrations/cwp` — save config
- `POST /api/admin/integrations/cwp/test` — test connection (invalidates cache)

---

## Section 2: Frontend UI

### Component Tree

```
IntegrationDetailView.vue        (existing — slug-based router shell)
└── CwpIntegrationPage.vue       (NEW — CWP-specific page)
    ├── CwpBrandHeader.vue        (NEW — dark banner, logo, animated status)
    ├── IntegrationConfigForm.vue (existing — reused as-is)
    └── CwpStatusSidebar.vue      (NEW — connection info, accounts, version)
```

### CwpBrandHeader.vue
- Background: `bg-gray-900` with `#1a73e8` left accent border
- Left: CWP icon + "CentOS Web Panel" title + version badge
- Right: animated status indicator
  - Connected: pulsing green dot (`animate-pulse bg-green-400`)
  - Error: static red dot (`bg-red-500`)
  - Unknown/loading: gray dot

### CwpStatusSidebar.vue
```
┌─────────────────────┐
│ ● Connected         │  ← animated badge (green/red/gray)
│                     │
│ Accounts            │
│ 142                 │  ← large number (text-3xl font-bold)
│                     │
│ CWP Version         │
│ v1.9.1              │
│                     │
│ Last tested         │
│ 2 minutes ago       │
│                     │
│ [Test Connection]   │
└─────────────────────┘
```

Fetches from `GET /api/admin/integrations/cwp/server-info`.  
Shows skeleton loaders while fetching.  
On fetch failure: renders "Status unknown" with gray dot — no crash.

### Integrations Grid — Auto-detect
Backend merges `showInIntegrations: true` plugins into the integrations list response.  
Frontend grid renders CWP under "Provisioning" category automatically.  
CWP card shows a `🔌 Plugin` badge to distinguish it from built-in integrations.  
No frontend changes to grid component needed.

### Route
`/integrations/cwp` — `IntegrationDetailView` detects `slug === "cwp"` and dynamically imports `CwpIntegrationPage`. All other slugs use the existing generic view.

If CWP plugin is not installed, `/integrations/cwp` redirects to `/plugins` with a toast: "Install the CWP plugin first."

---

## Section 3: Data Flow & Error Handling

### Data Flow

```
User opens /integrations/cwp
  → IntegrationDetailView detects slug="cwp"
  → loads CwpIntegrationPage
  → parallel fetch:
      ├── GET /api/admin/integrations/cwp        → config + field definitions
      └── GET /api/admin/integrations/cwp/server-info  → status sidebar data

User clicks [Save]
  → PUT /api/admin/integrations/cwp  { config }
  → success: toast "Saved"
  → re-fetch server-info (config may have changed)

User clicks [Test Connection]
  → POST /api/admin/integrations/cwp/test
  → animated spinner on button
  → success: sidebar updates → "Connected", last tested = "just now"
  → failure: red banner under form with error message
  → cache invalidated regardless of result
```

### Error States

| Scenario | UI Behavior |
|----------|-------------|
| server-info fetch fails | Sidebar: "Status unknown" gray dot — no crash |
| Test connection fails | Red banner under form: "Could not connect: {message}" |
| Save fails (validation) | Field-level errors from FluentValidation response |
| Plugin not installed | Redirect to `/plugins` with toast |
| CWP API timeout | server-info returns `connected: false`, message: "Timeout" |

### Caching
- `server-info` cached 5 min in `IMemoryCache` keyed by `"cwp:server-info"`
- `POST /test` handler calls `cache.Remove("cwp:server-info")` after test
- `PUT` (save config) also invalidates cache

---

## File Checklist

### Backend (new)
- `Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoQuery.cs`
- `Application/Admin/Integrations/Queries/GetCwpServerInfo/GetCwpServerInfoHandler.cs`
- `Application/Admin/Integrations/Queries/GetCwpServerInfo/CwpServerInfoDto.cs`
- `Providers.CWP/plugin.json` — add `showInIntegrations`, `integrationsCategory`
- `Infrastructure/Plugins/PluginLoader.cs` — handle `showInIntegrations` flag

### Frontend (new)
- `admin/src/modules/integrations/views/CwpIntegrationPage.vue`
- `admin/src/modules/integrations/components/CwpBrandHeader.vue`
- `admin/src/modules/integrations/components/CwpStatusSidebar.vue`
- `admin/src/modules/integrations/composables/useCwpServerInfo.ts`
- `admin/src/modules/integrations/types/cwp.types.ts`

### Frontend (modified)
- `admin/src/modules/integrations/views/IntegrationDetailView.vue` — add slug="cwp" branch
- `admin/src/modules/integrations/stores/integrationsStore.ts` — plugin badge support

---

## Out of Scope
- CWP account management (belongs to Services module)
- CWP package sync UI (future iteration)
- Logs tab (future iteration)
- Other plugin types (Payment, Registrar) follow same pattern in future specs
