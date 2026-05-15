# Plugin System — Design Spec

**Date:** 2026-04-19
**Branch:** main
**Scope:** Innovayse — extensible provider plugin system

---

## Overview

A plugin system that allows third-party developers to build and distribute new provider implementations (payment gateways, registrars, provisioning panels) as standalone packages. Innovayse ships official plugins; the community can build their own.

Plugins are ZIP files containing a compiled DLL and a `plugin.json` manifest. Admins install them via the admin panel. On server restart, plugins are discovered, validated, and registered automatically. The admin panel renders plugin config forms dynamically from manifest field definitions — no frontend deploy required.

---

## Architecture

```
Innovayse.SDK (NuGet package)
  ├── Interfaces: IProvisioningPlugin, IPaymentPlugin, IRegistrarPlugin
  ├── Base classes: ProvisioningProviderBase, PaymentGatewayBase, RegistrarProviderBase
  └── PluginManifest schema (for validation)

/plugins/{plugin-id}/
  ├── {PluginAssembly}.dll
  └── plugin.json

Backend (Innovayse.API startup)
  ├── PluginLoader — scans /plugins/, reads manifests, validates, loads assemblies, registers DI
  └── PluginRegistry — resolves active provider by category type

API endpoints (new)
  ├── GET  /api/admin/plugins           → list installed plugins with status
  ├── POST /api/admin/plugins/install   → upload ZIP, extract, validate manifest
  ├── DELETE /api/admin/plugins/{id}    → remove plugin files
  └── POST /api/admin/plugins/restart   → trigger graceful restart

Existing API (modified)
  └── GET /api/admin/integrations → now includes installed plugins alongside built-ins

Admin Panel (Vue 3)
  ├── /plugins            → Plugin Manager page (upload, list, remove)
  └── /integrations       → extended to show plugin-provided integrations
      └── /integrations/{slug} → IntegrationConfigForm already renders fields dynamically
```

---

## Plugin Manifest (`plugin.json`)

```json
{
  "id": "innovayse-cwp",
  "name": "CentOS Web Panel",
  "version": "1.0.0",
  "author": "Innovayse",
  "description": "Provisioning provider for CentOS Web Panel (CWP).",
  "type": "provisioning",
  "category": "Hosting / Provisioning",
  "color": "#1a73e8",
  "entryPoint": "Innovayse.Providers.CWP.CwpProvisioningProvider",
  "sdkVersion": "1.0",
  "fields": [
    { "key": "host",     "label": "Host",    "type": "text",   "required": true  },
    { "key": "port",     "label": "Port",    "type": "text",   "required": false },
    { "key": "api_key",  "label": "API Key", "type": "secret", "required": true  }
  ]
}
```

**Field types:** `text`, `secret`, `select`, `textarea`
**Plugin types:** `provisioning`, `payment`, `registrar`

---

## SDK — Base Classes

Third-party developers reference `Innovayse.SDK` NuGet and extend a base class:

```csharp
public class CwpProvisioningProvider : ProvisioningProviderBase
{
    public override async Task<ProvisioningResult> ProvisionAsync(
        ProvisionRequest req, CancellationToken ct)
    {
        var host   = GetConfig("host");    // reads from Settings table
        var apiKey = GetConfig("api_key"); // reads from Settings table
        // ... call CWP API
    }
}
```

**Base class provides:**
- `GetConfig(string key)` — reads `integration:{pluginId}:{key}` from Settings table
- `Logger` — `ILogger<T>` pre-injected, structured logging ready
- Default `NotSupportedException` for unimplemented optional methods
- Error wrapping — uncaught exceptions logged, returned as structured failure result

**SDK NuGet package:** `Innovayse.SDK` — contains interfaces + base classes only, no infrastructure dependencies.

---

## Plugin Discovery & Loading (Startup-time)

Loading happens **once at startup** — not hot-reload. This avoids runtime assembly isolation complexity while still giving full extensibility.

```
Application.CreateBuilder()
  → PluginLoader.DiscoverAndRegister(services, configuration)
    → foreach dir in /plugins/*/
      → read plugin.json → validate schema + sdkVersion
      → if invalid → log warning, skip (server still starts)
      → load assembly via Assembly.LoadFrom()
      → find type by entryPoint
      → register in DI keyed by plugin id
  → build app → start
```

**Failure handling:**
- Manifest missing or invalid → skip plugin, log warning
- DLL missing or load error → skip plugin, log error
- entryPoint type not found → skip plugin, log error
- Server always starts even if plugins fail — degraded mode, not crash

**Plugin registration in DI:**
```csharp
services.AddKeyedScoped<IProvisioningProvider>(manifest.Id, pluginType);
```

`PluginRegistry` resolves the active provider by reading `integration:{slug}:is_enabled` from Settings and returning the correct keyed service.

---

## API Changes

### New: Plugin Manager endpoints

**`POST /api/admin/plugins/install`**
- Accepts `multipart/form-data` ZIP upload
- Extracts to `/plugins/{manifest.id}/`
- Validates manifest (required fields, sdkVersion compatibility)
- Returns extracted manifest or validation error
- Does NOT restart — returns `{ "requiresRestart": true }`

**`DELETE /api/admin/plugins/{id}`**
- Removes `/plugins/{id}/` directory
- Returns `{ "requiresRestart": true }`

**`POST /api/admin/plugins/restart`**
- Triggers graceful `IHostApplicationLifetime.StopApplication()` — ASP.NET Core built-in, works on Windows, Linux, Docker
- Process manager (systemd, PM2, Docker restart policy) brings the process back up
- Admin panel shows "Restarting..." spinner, polls `/api/health` until back

**`GET /api/admin/plugins`**
- Returns installed plugins list with `isLoaded` (was DLL loaded successfully), `manifest`

### Modified: Integrations endpoint

**`GET /api/admin/integrations`**
- Now merges built-in integrations (Namecheap, cPanel, etc.) with loaded plugins
- Plugin entries sourced from `PluginRegistry.LoadedPlugins`
- Same `IntegrationListItemDto` shape — frontend unchanged

---

## Admin Panel Changes

### New page: `/plugins` — Plugin Manager

```
┌─────────────────────────────────────────────┐
│  Plugin Manager                             │
│                                             │
│  [Upload Plugin (.zip)]                     │
│                                             │
│  Installed Plugins                          │
│  ┌───────────────────────────────────────┐ │
│  │ ● CWP  v1.0.0  Innovayse  [Remove]   │ │
│  │   CentOS Web Panel — provisioning     │ │
│  └───────────────────────────────────────┘ │
│                                             │
│  ⚠ Restart required to apply changes       │
│  [Restart Server]                           │
└─────────────────────────────────────────────┘
```

**State:** `requiresRestart` flag shown after install/remove until restart completes.

### Modified: `/integrations`

After restart, plugin-provided integrations appear automatically in the correct category section. No code change — `IntegrationsView.vue` already renders all items from the store.

### Modified: `/integrations/{slug}` — Config page

`IntegrationConfigForm.vue` already renders fields dynamically from `INTEGRATION_META`. Change: field definitions now sourced from `GET /api/admin/integrations/{slug}` (API) instead of the static `integration.meta.ts` file. Backend returns `fields` array from plugin manifest or built-in metadata.

---

## File Structure

### New: `Innovayse.SDK` project

```
backend/src/Innovayse.SDK/
  Innovayse.SDK.csproj
  Plugins/
    IProvisioningPlugin.cs
    IPaymentPlugin.cs
    IRegistrarPlugin.cs
    PluginManifest.cs
    PluginField.cs
  Base/
    ProvisioningProviderBase.cs
    PaymentGatewayBase.cs
    RegistrarProviderBase.cs
```

### New: Plugin infrastructure in `Innovayse.Infrastructure`

```
backend/src/Innovayse.Infrastructure/
  Plugins/
    PluginLoader.cs        ← discovers, validates, loads assemblies
    PluginRegistry.cs      ← resolves active provider by type/slug
    PluginManifestReader.cs ← reads + validates plugin.json
```

### New: API endpoints in `Innovayse.API`

```
backend/src/Innovayse.API/
  Admin/
    PluginsController.cs
    Requests/
      InstallPluginRequest.cs  (multipart)
```

### New: Admin panel page

```
admin/src/modules/plugins/
  stores/
    pluginsStore.ts
  views/
    PluginsView.vue
  components/
    PluginCard.vue
    PluginUploader.vue
    RestartBanner.vue
```

### Modified files

```
admin/src/modules/integrations/
  stores/integrationsStore.ts  ← fetchOne() now returns fields from API
  components/IntegrationConfigForm.vue ← remove static INTEGRATION_META import

admin/src/router/index.ts ← add /plugins route
admin/src/components/layout/AppSidebar.vue ← add Plugin Manager nav item

backend/src/Innovayse.Infrastructure/DependencyInjection.cs ← call PluginLoader
backend/src/Innovayse.API/Admin/IntegrationsController.cs ← merge plugin metadata
```

---

## Official Plugins (distributed as NuGet + ZIP)

Innovayse ships these as official plugins in a public repository:

| Plugin | Type | Slug |
|---|---|---|
| CWP (CentOS Web Panel) | provisioning | cwp |
| Stripe | payment | stripe |
| PayPal | payment | paypal |
| ENOM | registrar | enom |
| ResellerClub | registrar | resellerclub |
| MaxMind | fraud | maxmind |

Built-in providers (Namecheap, cPanel, SMTP/MailKit) remain in Infrastructure — no migration needed.

---

## Security Model

- Plugins run in the same process with full trust — identical to WHMCS, WordPress, nopCommerce
- Official plugins: signed by Innovayse (manifest `signature` field — v2 roadmap)
- Community plugins: install at own risk, same as any NuGet package
- `sdkVersion` field prevents loading plugins built against incompatible SDK versions
- Plugin files stored outside web root — not directly accessible via HTTP

---

## Routing

```
/plugins                → PluginsView.vue (new)
/integrations           → IntegrationsView.vue (shows built-ins + plugins)
/integrations/{slug}    → IntegrationDetailView.vue (unchanged, fields from API)
```

Sidebar nav: **Plugin Manager** added above Apps & Integrations.

---

## Out of Scope (v1)

- Hot-reload without restart
- Plugin marketplace / discovery UI
- Plugin signing / verification
- Plugin-to-plugin dependencies
- Custom API endpoints from plugins
- Custom scheduled jobs from plugins
